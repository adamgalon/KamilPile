# Metryki pali — generator

Windows desktop app (POC) that turns a pile schedule (`tabelka z palami`) into
a printable as-built pile record workbook (`metryki pali`).

Input `tabelka z palami.xlsx` → Output `Metryki pali.xlsx` → print / export to PDF.

## Running it

**Offline, no .NET needed on the machine:**

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

produces a single self-contained `publish\MetrykiPali.exe` (~51 MB) that you can
copy to any Windows 10/11 machine and double-click.

**From source (needs .NET 10 SDK):**

```powershell
dotnet run --project src\MetrykiPali
```

## How to use

1. **Wczytaj tabelkę** — pick the source file. Accepts `.xlsx`, `.xls`, `.csv`
   and `.pdf`. Expected columns, in order:

   | numer od | numer do | średnica [m] | długość pala [m] | zbrojenie |
   |---|---|---|---|---|
   | 1 | 10 | 0.4 | 7 | Brak |
   | 11 | 28 | 0.4 | 8 | Brak |

   Header rows and blank lines are skipped automatically. Both `0.4` and `0,4`
   decimal separators are accepted.

2. **Check the header fields** — budowa, wykonawca, metoda, data, betoniarnia.
   They are pre-filled with the values from the reference documentation and are
   written onto every page.

3. **Review the piles** in the grid. Ranges are expanded into individual piles;
   `Dł. wykonana` defaults to the design length and the cells are editable, so
   you can correct any pile that was driven differently before generating.

4. **Generuj metryki (.xlsx)** — choose where to save. Then **Otwórz
   wygenerowany plik** to check it, and print or *Save as PDF* from Excel.

## Concrete volume

`Ilość betonu wbudowanego` is computed as:

```
V = π/4 · D² · L · k
```

where `k` is the overbreak coefficient (**Wsp. betonu**, default `1.30`).

`k = 1.30` reproduces the volumes in the reference documentation for a 0.4 m CFA
pile:

| L [m] | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|
| computed [m³] | 0.98 | 1.14 | 1.31 | 1.47 | 1.63 | 1.80 | 1.96 |

The reference records vary between roughly 1.18 and 1.46 from day to day, because
they are measured quantities rather than calculated ones. Change **Wsp. betonu**
to match a particular pour — every volume in the grid recalculates immediately.

## Output layout

Matches the reference documentation: **12 piles per A4 portrait page**, laid out
on a repeating 48-row block so page breaks fall exactly where they do in the
original, with the same page header (`BUDOWA … / DOKUMENTACJA POWYKONAWCZA`),
footer (`GREIFBAU SP. Z O.O.` + page number), margins and seven table rows:

```
Numer pala | Średnica pala [m] | Długość pala wg projektu [m]
Długość wykonanego pala [m] | Ilość betonu wbudowanego [m3]
Beton z betoniarni: | Zbrojenie:
```

plus `UWAGI:` and `KIEROWNIK ROBÓT PALOWYCH:` blocks.

## Verified against the real data

`tabelka z palami.xlsx` and `tabelka z palami1.pdf` both parse to **93 ranges →
969 piles → 81 pages**. Exported through Excel the result is an 81-page PDF with
12 piles on each full page and 9 on the last.

## Project layout

```
src/MetrykiPali/
  Model.cs             PileRange, Pile, MetrykaSettings, volume formula
  PileTableReader.cs   reads .xlsx/.xls/.csv/.pdf, expands ranges into piles
  MetrykaWriter.cs     writes the paginated METRYKA PALI workbook
  MainForm.cs          the UI
publish.ps1            builds the standalone offline .exe
```

## Known limits (it is a POC)

- Output is `.xlsx`; PDF is one *Save as PDF* away in Excel but is not generated
  directly by the app.
- Every page carries the same date. The reference documentation groups piles by
  the day they were poured; that grouping is not reconstructed from the schedule.
- Piles are emitted in schedule order, not in the order they were actually
  executed.
- The source table must have its five columns in the order shown above; the app
  does not yet let you map columns by name.
