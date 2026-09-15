using ClosedXML.Excel;

namespace MetrykiPali.Tests;

/// <summary>
/// The job as it actually runs: load the schedule once, log piles day by day
/// over several sessions, then generate every metryka at the end.
/// </summary>
public sealed class WorkflowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mpali-flow-" + Guid.NewGuid().ToString("N"));

    public WorkflowTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    /// <summary>Logs piles against a date, the way the journal box does.</summary>
    private static void Log(ProjectState project, string piles, DateTime date)
    {
        var index = project.Piles.ToDictionary(p => p.Number);
        foreach (var number in PileNumbers.Parse(piles))
            if (index.TryGetValue(number, out var pile)) pile.Executed = date;
    }

    private static List<WorkDay> Journal(ProjectState project) => project.Piles
        .Where(p => p.Executed is not null)
        .GroupBy(p => p.Executed!.Value.Date)
        .OrderBy(g => g.Key)
        .Select(g => new WorkDay { Date = g.Key, Piles = g.OrderBy(p => p.Number).ToList() })
        .ToList();

    [Fact]
    public void Three_days_of_work_survive_two_restarts_and_generate_together()
    {
        var projectPath = Path.Combine(_dir, "projekt.mpali");
        var settings = new MetrykaSettings { ConcreteFactor = 1.30 };

        // --- day one: load the schedule and log the first piles --------------
        var ranges = PileTableReader.Read(Fixture("tabelka-testowa.xlsx")).ToList();
        var project = new ProjectState
        {
            SourcePath = Fixture("tabelka-testowa.xlsx"),
            Settings = settings,
            Ranges = ranges,
            Piles = PileTableReader.Expand(ranges, settings)
        };
        Log(project, "1-12", new DateTime(2022, 9, 12));
        ProjectStore.Save(projectPath, project);

        // --- restart: the app reopens the saved project ----------------------
        project = ProjectStore.Load(projectPath)!;
        Assert.Equal(60, project.Piles.Count);
        Assert.Equal(12, project.Piles.Count(p => p.Executed is not null));

        Log(project, "13-24, 25-30", new DateTime(2022, 9, 13));
        ProjectStore.Save(projectPath, project);

        // --- restart again ---------------------------------------------------
        project = ProjectStore.Load(projectPath)!;
        Assert.Equal(30, project.Piles.Count(p => p.Executed is not null));

        Log(project, "31-42", new DateTime(2022, 9, 14));
        ProjectStore.Save(projectPath, project);

        // --- the end of the job: generate everything at once -----------------
        project = ProjectStore.Load(projectPath)!;
        var days = Journal(project);

        Assert.Equal(3, days.Count);
        Assert.Equal(new[] { 12, 18, 12 }, days.Select(d => d.Piles.Count));
        Assert.Equal(18, project.Piles.Count(p => p.Executed is null));   // 43-60 still to do

        var output = Path.Combine(_dir, "metryki.xlsx");
        MetrykaWriter.Write(output, days, project.Settings);

        using var wb = new XLWorkbook(output);
        var ws = wb.Worksheet(1);

        // 12 + (12 + 6) + 12 piles -> four pages
        Assert.Equal(3, ws.PageSetup.RowBreaks.Count);
        Assert.Equal(new DateTime(2022, 9, 12), ws.Cell(12, 3).GetDateTime());
        Assert.Equal(new DateTime(2022, 9, 13), ws.Cell(48 + 12, 3).GetDateTime());
        Assert.Equal(new DateTime(2022, 9, 13), ws.Cell(96 + 12, 3).GetDateTime());
        Assert.Equal(new DateTime(2022, 9, 14), ws.Cell(144 + 12, 3).GetDateTime());
    }

    [Fact]
    public void Reloading_a_corrected_schedule_keeps_the_journal()
    {
        var settings = new MetrykaSettings();
        var ranges = PileTableReader.Read(Fixture("tabelka-testowa.xlsx")).ToList();
        var before = new ProjectState { Piles = PileTableReader.Expand(ranges, settings) };

        Log(before, "1-12", new DateTime(2022, 9, 12));
        var original = before.Piles;

        // The designer reissues the schedule; the piles are rebuilt from it.
        var reloaded = PileTableReader.Expand(ranges, settings);
        var previous = original.ToDictionary(p => p.Number);
        foreach (var pile in reloaded)
            if (previous.TryGetValue(pile.Number, out var old) && old.Executed is not null)
                pile.Executed = old.Executed;

        Assert.Equal(12, reloaded.Count(p => p.Executed is not null));
        Assert.Equal(new DateTime(2022, 9, 12), reloaded[0].Executed);
    }

    [Fact]
    public void Moving_a_pile_to_another_day_does_not_duplicate_it()
    {
        var settings = new MetrykaSettings();
        var project = new ProjectState
        {
            Piles = PileTableReader.Expand(PileTableReader.Read(Fixture("tabelka-testowa.xlsx")), settings)
        };

        Log(project, "1-5", new DateTime(2022, 9, 12));
        Log(project, "3", new DateTime(2022, 9, 13));      // pile 3 was actually poured a day later

        var days = Journal(project);

        Assert.Equal(2, days.Count);
        Assert.Equal(new[] { 1, 2, 4, 5 }, days[0].Piles.Select(p => p.Number));
        Assert.Equal(new[] { 3 }, days[1].Piles.Select(p => p.Number));
        Assert.Equal(5, days.Sum(d => d.Piles.Count));
    }

    [Fact]
    public void Concrete_totals_add_up_per_day()
    {
        var settings = new MetrykaSettings { ConcreteFactor = 1.30 };
        var project = new ProjectState
        {
            Piles = PileTableReader.Expand(PileTableReader.Read(Fixture("tabelka-testowa.xlsx")), settings)
        };

        Log(project, "1-12", new DateTime(2022, 9, 12));    // all 7 m piles -> 1.14 each

        var day = Journal(project).Single();

        Assert.Equal(12 * 1.14, day.Piles.Sum(p => p.Concrete), 2);
    }
}
