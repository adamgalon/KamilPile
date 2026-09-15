using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

using MetrykiPali.Model;

namespace MetrykiPali.Services;

/// <summary>
/// Reads the source table ("tabelka z palami") from .xlsx / .xls / .csv / .pdf.
/// Expected columns: od | do | średnica | długość | zbrojenie.
/// </summary>
public sealed class PileTableReader : IScheduleReader
{
    public IReadOnlyList<PileRange> Read(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var ranges = ext switch
        {
            ".xlsx" or ".xlsm" or ".xls" => ReadExcel(path),
            ".csv" or ".txt" => ReadCsv(path),
            ".pdf" => ReadPdf(path),
            _ => throw new NotSupportedException($"Nieobsługiwany format pliku: {ext}")
        };

        if (ranges.Count == 0)
            throw new InvalidDataException(
                "Nie znaleziono żadnych zakresów pali. Oczekiwane kolumny: " +
                "numer od | numer do | średnica | długość pala | zbrojenie.");

        return ranges;
    }

    // ---------------------------------------------------------------- Excel

    private static List<PileRange> ReadExcel(string path)
    {
        // Copy first: the source file is often open in Excel / synced by OneDrive,
        // both of which hold a lock that would make a direct open fail.
        var temp = Path.Combine(Path.GetTempPath(), $"metryki_{Guid.NewGuid():N}{Path.GetExtension(path)}");
        File.Copy(path, temp, overwrite: true);
        try
        {
            using var wb = new XLWorkbook(temp);
            var ws = wb.Worksheets.First();
            var result = new List<PileRange>();

            foreach (var row in ws.RangeUsed()?.RowsUsed() ?? Enumerable.Empty<IXLRangeRow>())
            {
                var cells = Enumerable.Range(1, 5).Select(i => row.Cell(i)).ToArray();
                var range = TryBuildRange(
                    CellText(cells[0]), CellText(cells[1]),
                    CellText(cells[2]), CellText(cells[3]), CellText(cells[4]));
                if (range is not null) result.Add(range);
            }

            return result;
        }
        finally
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }
    }

    private static string CellText(IXLCell cell)
        => cell.IsEmpty() ? "" : cell.GetFormattedString().Trim();

    // ------------------------------------------------------------------ CSV

    private static List<PileRange> ReadCsv(string path)
    {
        var result = new List<PileRange>();
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            var parts = line.Split(FieldSeparator(line), StringSplitOptions.None);
            if (parts.Length < 4) continue;
            var range = TryBuildRange(
                parts[0], parts[1], parts[2], parts[3],
                parts.Length > 4 ? parts[4] : "Brak");
            if (range is not null) result.Add(range);
        }
        return result;
    }

    /// <summary>
    /// Picks the separator for one line. A semicolon or a tab wins over a comma,
    /// because Excel exports on a Polish machine separate fields with semicolons
    /// and write decimals with commas - splitting such a line on commas as well
    /// would tear "0,4" into two fields and lose the row.
    /// </summary>
    private static char[] FieldSeparator(string line)
    {
        if (line.Contains(';')) return new[] { ';' };
        if (line.Contains('\t')) return new[] { '\t' };
        return new[] { ',' };
    }

    // ------------------------------------------------------------------ PDF

    /// <summary>
    /// Groups PDF words into visual rows by their vertical position, then reads
    /// the first five tokens of each row. Uses word coordinates rather than the
    /// raw text stream, so columns that render without separating spaces
    /// (e.g. "0.4" + "8") are still kept apart.
    /// </summary>
    private static List<PileRange> ReadPdf(string path)
    {
        var result = new List<PileRange>();
        using var doc = PdfDocument.Open(path);

        foreach (var page in doc.GetPages())
        {
            foreach (var line in GroupIntoLines(page.GetWords()))
            {
                if (line.Count < 4) continue;
                var t = line.Select(w => w.Text.Trim()).ToList();
                var range = TryBuildRange(t[0], t[1], t[2], t[3], t.Count > 4 ? t[4] : "Brak");
                if (range is not null) result.Add(range);
            }
        }

        return result;
    }

    private static IEnumerable<List<Word>> GroupIntoLines(IEnumerable<Word> words)
    {
        const double tolerance = 4.0; // points; rows in these tables are ~11 pt apart

        return words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / tolerance))
            .OrderByDescending(g => g.Key)
            .Select(g => g.OrderBy(w => w.BoundingBox.Left).ToList());
    }

    // --------------------------------------------------------------- parsing

    private static PileRange? TryBuildRange(string from, string to, string diameter, string length, string reinforcement)
    {
        if (!TryNumber(from, out var f) || !TryNumber(to, out var t)) return null;
        if (!TryNumber(diameter, out var d) || !TryNumber(length, out var l)) return null;

        var fromNo = (int)Math.Round(f);
        var toNo = (int)Math.Round(t);

        // Guard against header rows and junk lines that happen to parse as numbers.
        if (fromNo <= 0 || toNo < fromNo) return null;
        if (d <= 0 || d > 5) return null;      // diameter in metres
        if (l <= 0 || l > 100) return null;    // length in metres

        return new PileRange
        {
            From = fromNo,
            To = toNo,
            Diameter = d,
            Length = l,
            Reinforcement = string.IsNullOrWhiteSpace(reinforcement) ? "Brak" : reinforcement.Trim()
        };
    }

    /// <summary>Parses a number written with either a dot or a comma decimal separator.</summary>
    private static bool TryNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var cleaned = text.Trim().Replace(" ", "").Replace('\u00A0', ' ').Trim().Replace(',', '.');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
