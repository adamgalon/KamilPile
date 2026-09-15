namespace MetrykiPali.Model;

/// <summary>
/// What adding a set of pile numbers to a day would do, worked out before
/// anything changes so the presenter can ask the user about the awkward parts
/// (numbers that are not in the schedule, piles already logged on another day)
/// and then apply exactly what was agreed.
/// </summary>
public sealed record JournalPlan(
    DateTime Date,
    IReadOnlyList<int> Known,
    IReadOnlyList<int> Unknown,
    IReadOnlyList<int> Moved)
{
    public bool HasAnythingToDo => Known.Count > 0;
}

/// <summary>
/// The work journal: which piles were poured on which day.
///
/// The date lives on the pile itself (<see cref="Pile.Executed"/>), so there is
/// one source of truth - no second list that could drift out of step with it.
/// </summary>
public sealed class Journal
{
    private readonly List<Pile> _piles;

    public Journal(List<Pile> piles) => _piles = piles;

    public IReadOnlyList<Pile> Piles => _piles;
    public int Total => _piles.Count;
    public int Assigned => _piles.Count(p => p.Executed is not null);
    public int Outstanding => Total - Assigned;

    /// <summary>Works out what adding these numbers to this day would mean.</summary>
    public JournalPlan Plan(IEnumerable<int> numbers, DateTime date)
    {
        date = date.Date;
        var byNumber = _piles.ToDictionary(p => p.Number);
        var known = new List<int>();
        var unknown = new List<int>();
        var moved = new List<int>();

        foreach (var number in numbers.Distinct().OrderBy(n => n))
        {
            if (!byNumber.TryGetValue(number, out var pile)) { unknown.Add(number); continue; }

            known.Add(number);
            if (pile.Executed is not null && pile.Executed.Value.Date != date) moved.Add(number);
        }

        return new JournalPlan(date, known, unknown, moved);
    }

    /// <summary>Applies a plan and returns how many piles were logged.</summary>
    public int Apply(JournalPlan plan)
    {
        var byNumber = _piles.ToDictionary(p => p.Number);
        var applied = 0;

        foreach (var number in plan.Known)
        {
            if (!byNumber.TryGetValue(number, out var pile)) continue;
            pile.Executed = plan.Date;
            applied++;
        }

        return applied;
    }

    /// <summary>Returns a day's piles to the pool of piles with no date.</summary>
    public int RemoveDay(DateTime date)
    {
        date = date.Date;
        var cleared = 0;

        foreach (var pile in _piles.Where(p => p.Executed?.Date == date))
        {
            pile.Executed = null;
            cleared++;
        }

        return cleared;
    }

    /// <summary>The logged days, in date order, each with its piles in number order.</summary>
    public List<WorkDay> Days() => _piles
        .Where(p => p.Executed is not null)
        .GroupBy(p => p.Executed!.Value.Date)
        .OrderBy(g => g.Key)
        .Select(g => new WorkDay { Date = g.Key, Piles = g.OrderBy(p => p.Number).ToList() })
        .ToList();

    /// <summary>The same days rendered for the journal grid.</summary>
    public List<JournalEntry> Entries(int pilesPerPage)
    {
        var perPage = Math.Max(1, pilesPerPage);

        return Days().Select(day => new JournalEntry
        {
            Data = day.Date,
            Pale = PileNumbers.Format(day.Piles.Select(p => p.Number)),
            Ilosc = day.Piles.Count,
            Beton = Math.Round(day.Piles.Sum(p => p.Concrete), 2),
            Strony = (int)Math.Ceiling(day.Piles.Count / (double)perPage)
        }).ToList();
    }

    /// <summary>Recomputes every concrete volume after the coefficient changes.</summary>
    public void RecalculateConcrete(double factor)
    {
        foreach (var pile in _piles)
            pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, factor);
    }

    /// <summary>Recomputes one pile, after its length or diameter was corrected.</summary>
    public void RecalculateConcrete(Pile pile, double factor)
        => pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, factor);

    public void SetConcretePlant(string plant)
    {
        foreach (var pile in _piles) pile.ConcretePlant = plant;
    }
}
