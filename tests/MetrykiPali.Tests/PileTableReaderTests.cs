namespace MetrykiPali.Tests;

public class PileTableReaderTests
{
    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    // ------------------------------------------------------------------ CSV

    [Fact]
    public void Reads_a_plain_csv_schedule()
    {
        var ranges = Reader.Read(Fixture("tabelka-podstawowa.csv"));

        Assert.Equal(5, ranges.Count);
        Assert.Equal(1, ranges[0].From);
        Assert.Equal(10, ranges[0].To);
        Assert.Equal(0.4, ranges[0].Diameter);
        Assert.Equal(7, ranges[0].Length);
        Assert.Equal("Brak", ranges[0].Reinforcement);
        Assert.Equal(7.5, ranges[3].Length);       // fractional design length
        Assert.Equal(44, ranges[^1].To);
    }

    [Fact]
    public void Accepts_comma_decimal_separators()
    {
        var ranges = Reader.Read(Fixture("tabelka-przecinki.csv"));

        Assert.Equal(3, ranges.Count);
        Assert.Equal(0.4, ranges[0].Diameter);
        Assert.Equal(8.5, ranges[0].Length);
        Assert.Equal(0.5, ranges[1].Diameter);
        Assert.Equal("Kosz zbrojeniowy", ranges[1].Reinforcement);
        Assert.Equal(0.88, ranges[2].Diameter);
    }

    [Fact]
    public void Accepts_comma_separated_fields_with_dot_decimals()
    {
        // The other common export shape. Guards the semicolon handling from
        // breaking plain comma-separated files.
        var ranges = Reader.Read(Fixture("tabelka-angielska.csv"));

        Assert.Equal(3, ranges.Count);
        Assert.Equal(0.4, ranges[0].Diameter);
        Assert.Equal(7, ranges[0].Length);
        Assert.Equal(0.5, ranges[2].Diameter);
        Assert.Equal("Kosz", ranges[2].Reinforcement);
    }

    [Fact]
    public void Skips_headers_blank_lines_notes_and_totals()
    {
        var ranges = Reader.Read(Fixture("tabelka-smieci.csv"));

        Assert.Equal(3, ranges.Count);
        Assert.Equal(new[] { 1, 11, 31 }, ranges.Select(r => r.From));
        Assert.Equal(new[] { 10, 20, 35 }, ranges.Select(r => r.To));
    }

    // -------------------------------------------------------- Excel and PDF

    [Fact]
    public void Reads_an_xlsx_schedule()
    {
        var ranges = Reader.Read(Fixture("tabelka-testowa.xlsx"));

        Assert.Equal(6, ranges.Count);
        Assert.Equal(60, ranges.Sum(r => r.Count));
        Assert.Equal(7.5, ranges[4].Length);
        Assert.All(ranges, r => Assert.Equal(0.4, r.Diameter));
    }

    [Fact]
    public void Reads_a_pdf_schedule_identically_to_the_xlsx()
    {
        var fromExcel = Reader.Read(Fixture("tabelka-testowa.xlsx"));
        var fromPdf = Reader.Read(Fixture("tabelka-testowa.pdf"));

        Assert.Equal(
            fromExcel.Select(r => (r.From, r.To, r.Diameter, r.Length)),
            fromPdf.Select(r => (r.From, r.To, r.Diameter, r.Length)));
    }

    [Fact]
    public void Reads_an_xlsx_that_another_program_holds_open()
    {
        // The schedule is usually still open in Excel, or being synced by
        // OneDrive; the reader must not fail on the resulting lock.
        var path = Fixture("tabelka-testowa.xlsx");
        using var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal(6, Reader.Read(path).Count);
    }

    // --------------------------------------------------------------- errors

    [Fact]
    public void Rejects_an_unsupported_file_type()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");
        File.WriteAllText(path, "nie tabelka");
        try
        {
            Assert.Throws<NotSupportedException>(() => Reader.Read(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Explains_itself_when_a_file_holds_no_schedule()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllText(path, "jakis tekst\nbez danych\n");
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => Reader.Read(path));
            Assert.Contains("rednica", error.Message);   // names the expected columns
        }
        finally { File.Delete(path); }
    }

    // ------------------------------------------------------------ expansion

    [Fact]
    public void Expand_turns_ranges_into_individual_piles()
    {
        var settings = new MetrykaSettings { ConcreteFactor = 1.30, Betoniarnia = "Bosta" };
        var piles = PileSchedule.Expand(Reader.Read(Fixture("tabelka-testowa.xlsx")), settings);

        Assert.Equal(60, piles.Count);
        Assert.Equal(Enumerable.Range(1, 60), piles.Select(p => p.Number));

        var first = piles[0];
        Assert.Equal(7, first.DesignLength);
        Assert.Equal(7, first.ActualLength);          // defaults to the design length
        Assert.Equal(1.14, first.Concrete);           // computed, not copied
        Assert.Equal("Bosta", first.ConcretePlant);
        Assert.Equal("Brak", first.Reinforcement);
        Assert.Null(first.Executed);                  // nothing is in the journal yet
    }

    [Fact]
    public void Expand_uses_the_configured_coefficient()
    {
        var ranges = Reader.Read(Fixture("tabelka-podstawowa.csv"));

        var standard = PileSchedule.Expand(ranges, new MetrykaSettings { ConcreteFactor = 1.30 });
        var lean = PileSchedule.Expand(ranges, new MetrykaSettings { ConcreteFactor = 1.05 });

        Assert.Equal(1.14, standard[0].Concrete);
        Assert.Equal(0.92, lean[0].Concrete);
    }

    [Fact]
    public void Expand_covers_every_number_in_every_range()
    {
        var ranges = Reader.Read(Fixture("tabelka-smieci.csv"));
        var piles = PileSchedule.Expand(ranges, new MetrykaSettings());

        Assert.Equal(25, piles.Count);                       // 10 + 10 + 5
        Assert.DoesNotContain(piles, p => p.Number is >= 21 and <= 30);   // the withheld block
    }
}
