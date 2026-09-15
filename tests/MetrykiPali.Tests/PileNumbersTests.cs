namespace MetrykiPali.Tests;

public class PileNumbersTests
{
    [Theory]
    [InlineData("1-10, 25, 30-33", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 25, 30, 31, 32, 33 })]
    [InlineData("5", new[] { 5 })]
    [InlineData("7-9", new[] { 7, 8, 9 })]
    [InlineData("3,1,2", new[] { 1, 2, 3 })]                  // sorted
    [InlineData("3,3,3,4", new[] { 3, 4 })]                    // de-duplicated
    [InlineData(" 5 ;7\t6 ", new[] { 5, 6, 7 })]               // mixed separators
    [InlineData("1–3", new[] { 1, 2, 3 })]                // en-dash
    [InlineData("1—3", new[] { 1, 2, 3 })]                // em-dash
    [InlineData("10-10", new[] { 10 })]                        // degenerate range
    public void Parse_reads_site_notation(string input, int[] expected)
        => Assert.Equal(expected, PileNumbers.Parse(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_treats_nothing_as_no_piles(string? input)
        => Assert.Empty(PileNumbers.Parse(input));

    [Theory]
    [InlineData("10-1")]      // reversed
    [InlineData("abc")]
    [InlineData("1-abc")]
    [InlineData("0")]         // pile numbers start at 1
    [InlineData("-5")]        // not a range, and not a valid number
    [InlineData("1,,,x")]
    public void Parse_rejects_what_it_cannot_read(string input)
        => Assert.Throws<FormatException>(() => PileNumbers.Parse(input));

    [Fact]
    public void Parse_names_the_offending_fragment()
    {
        var error = Assert.Throws<FormatException>(() => PileNumbers.Parse("1-10, zonk, 20"));
        Assert.Contains("zonk", error.Message);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, "1-3")]
    [InlineData(new[] { 1, 3, 5 }, "1, 3, 5")]
    [InlineData(new[] { 42 }, "42")]
    [InlineData(new[] { 1, 2, 3, 7, 9, 10, 11 }, "1-3, 7, 9-11")]
    [InlineData(new int[0], "")]
    public void Format_collapses_runs(int[] numbers, string expected)
        => Assert.Equal(expected, PileNumbers.Format(numbers));

    [Fact]
    public void Format_sorts_and_deduplicates()
        => Assert.Equal("1-3", PileNumbers.Format(new[] { 3, 1, 2, 2, 1 }));

    [Theory]
    [InlineData("1-10, 25, 30-33")]
    [InlineData("1, 3, 5, 7")]
    [InlineData("100-200")]
    public void Format_round_trips_through_Parse(string text)
        => Assert.Equal(text, PileNumbers.Format(PileNumbers.Parse(text)));
}
