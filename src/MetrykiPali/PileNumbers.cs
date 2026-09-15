using System.Globalization;
using System.Text;

namespace MetrykiPali;

/// <summary>
/// Parses and formats pile number lists the way they are written on site,
/// e.g. "1-10, 25, 30-33".
/// </summary>
public static class PileNumbers
{
    private static readonly char[] Separators = { ',', ';', ' ', '\t', '\r', '\n' };
    private static readonly char[] RangeDashes = { '-', '–', '—' };

    /// <summary>
    /// Expands a written list into distinct, ascending pile numbers.
    /// Throws <see cref="FormatException"/> on anything it cannot read, so the
    /// caller can show the offending fragment to the user.
    /// </summary>
    public static List<int> Parse(string? text)
    {
        var numbers = new SortedSet<int>();
        if (string.IsNullOrWhiteSpace(text)) return numbers.ToList();

        foreach (var token in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var dash = token.IndexOfAny(RangeDashes, 1);
            if (dash < 0)
            {
                numbers.Add(ParseNumber(token, token));
                continue;
            }

            var from = ParseNumber(token[..dash], token);
            var to = ParseNumber(token[(dash + 1)..], token);
            if (to < from)
                throw new FormatException($"Zakres \"{token}\" jest odwrócony — początek jest większy niż koniec.");

            for (var n = from; n <= to; n++) numbers.Add(n);
        }

        return numbers.ToList();
    }

    private static int ParseNumber(string part, string token)
    {
        var trimmed = part.Trim();
        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new FormatException($"\"{token}\" nie jest poprawnym numerem pala ani zakresem.");

        return value;
    }

    /// <summary>Collapses pile numbers back into a compact "1-10, 25, 30-33" string.</summary>
    public static string Format(IEnumerable<int> numbers)
    {
        var sorted = numbers.Distinct().OrderBy(n => n).ToList();
        if (sorted.Count == 0) return "";

        var text = new StringBuilder();
        var start = sorted[0];
        var previous = start;

        for (var i = 1; i <= sorted.Count; i++)
        {
            var isBreak = i == sorted.Count || sorted[i] != previous + 1;
            if (isBreak)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(start == previous ? start.ToString() : $"{start}-{previous}");

                if (i < sorted.Count) start = sorted[i];
            }

            if (i < sorted.Count) previous = sorted[i];
        }

        return text.ToString();
    }
}
