namespace MetrykiPali.Tests;

public class PaginationTests
{
    private static Pile P(int number, DateTime? day = null) => new()
    {
        Number = number,
        Diameter = 0.4,
        DesignLength = 9,
        ActualLength = 9,
        Concrete = 1.47,
        ConcretePlant = "Bosta",
        Reinforcement = "Brak",
        Executed = day
    };

    private static WorkDay Day(DateTime date, params int[] numbers)
        => new() { Date = date, Piles = numbers.Select(n => P(n, date)).ToList() };

    private static readonly DateTime D12 = new(2022, 9, 12);
    private static readonly DateTime D13 = new(2022, 9, 13);
    private static readonly DateTime D14 = new(2022, 9, 14);

    [Fact]
    public void A_full_day_is_one_page()
    {
        var pages = MetrykaWriter.Paginate(new[] { Day(D12, Enumerable.Range(1, 12).ToArray()) }, 12);

        Assert.Single(pages);
        Assert.Equal(12, pages[0].Piles.Count);
        Assert.Equal(D12, pages[0].Date);
    }

    [Fact]
    public void A_long_day_spills_onto_further_pages()
    {
        var pages = MetrykaWriter.Paginate(new[] { Day(D13, Enumerable.Range(1, 18).ToArray()) }, 12);

        Assert.Equal(2, pages.Count);
        Assert.Equal(12, pages[0].Piles.Count);
        Assert.Equal(6, pages[1].Piles.Count);
        Assert.All(pages, p => Assert.Equal(D13, p.Date));
    }

    /// <summary>
    /// The whole point of the journal: a metryka carries one date, so piles from
    /// two days must never share a sheet even when the first day is short.
    /// </summary>
    [Fact]
    public void A_page_never_mixes_two_days()
    {
        var pages = MetrykaWriter.Paginate(new[]
        {
            Day(D12, 1, 2, 3),
            Day(D13, 4, 5, 6)
        }, 12);

        Assert.Equal(2, pages.Count);
        Assert.All(pages, page => Assert.All(page.Piles, pile => Assert.Equal(page.Date, pile.Executed)));
    }

    [Fact]
    public void Days_come_out_in_date_order_however_they_were_logged()
    {
        var pages = MetrykaWriter.Paginate(new[]
        {
            Day(D14, 30),
            Day(D12, 10),
            Day(D13, 20)
        }, 12);

        Assert.Equal(new[] { D12, D13, D14 }, pages.Select(p => p.Date));
    }

    [Fact]
    public void Piles_are_numbered_upwards_within_a_day()
    {
        var pages = MetrykaWriter.Paginate(new[] { Day(D12, 9, 3, 7, 1) }, 12);

        Assert.Equal(new[] { 1, 3, 7, 9 }, pages[0].Piles.Select(p => p.Number));
    }

    [Fact]
    public void Days_with_no_piles_produce_no_pages()
    {
        var pages = MetrykaWriter.Paginate(new[]
        {
            Day(D12, 1, 2),
            new WorkDay { Date = D13 }
        }, 12);

        Assert.Single(pages);
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(6, 2)]
    [InlineData(12, 1)]
    public void Page_size_is_configurable(int perPage, int expectedPages)
    {
        var pages = MetrykaWriter.Paginate(new[] { Day(D12, Enumerable.Range(1, 12).ToArray()) }, perPage);

        Assert.Equal(expectedPages, pages.Count);
    }

    [Fact]
    public void A_nonsense_page_size_still_produces_pages()
    {
        var pages = MetrykaWriter.Paginate(new[] { Day(D12, 1, 2) }, 0);

        Assert.Equal(2, pages.Count);
    }

    [Fact]
    public void Nothing_logged_means_nothing_to_paginate()
        => Assert.Empty(MetrykaWriter.Paginate(Array.Empty<WorkDay>(), 12));

    [Fact]
    public void Writing_an_empty_journal_is_refused()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");

        var error = Assert.Throws<InvalidOperationException>(
            () => MetrykaWriter.Write(path, Array.Empty<WorkDay>(), new MetrykaSettings()));

        Assert.Contains("Dziennik", error.Message);
        Assert.False(File.Exists(path));
    }
}
