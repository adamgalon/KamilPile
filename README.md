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

2. **Check the header fields** — budowa, wykonawca, metoda, betoniarnia.
   They are pre-filled with the values from the reference documentation and are
   written onto every page.

3. **Log each day's work.** Pick the date, type the piles you completed and press
   **Dodaj do dziennika** (or just Enter):

   ```
   Data wykonania: 13.09.2022    Pale: 11-16, 63-66, 77-84
   ```

   Ranges and single numbers, separated by commas, semicolons or spaces. You can
   also select rows on the **Pale** tab and use **Dodaj zaznaczone z listy pali**.

   Close the app and come back tomorrow — the journal is still there. Add the
   next day, and the next, for as long as the job runs.

4. **Review the piles** on the **Pale** tab. `Dł. wykonana` defaults to the
   design length and is editable, so you can correct any pile that was driven
   differently; its concrete volume recalculates immediately.

5. **Generuj metryki (.xlsx)** — generates every day in the journal in one go.
   Then **Otwórz wygenerowany plik** to check it, and print or *Save as PDF*
   from Excel.

## The work journal

Each pile carries the date it was poured. The **Dziennik (dni)** tab shows one
row per day — date, the piles in compact form, count, concrete and how many
metryka pages that day will produce.

- **Days never share a page.** Each metryka carries one DATA value, the day those
  piles were actually poured, exactly as in the reference documentation. A day of
  18 piles produces two pages (12 + 6), not one and a half.
- **Re-entering a pile with a different date moves it**, after asking. Useful when
  a number was logged against the wrong day.
- **Usuń zaznaczony dzień** returns that day's piles to the unassigned pool.
- Piles with no date are **not** written to the metryki. The status bar always
  shows how many are still outstanding, and you are warned before generating.
- Reloading a corrected schedule **keeps the journal** — pour dates are matched
  back by pile number, so weeks of site records are not lost.

## Where the data is kept

The journal is saved automatically — on every change and on exit — to:

```
%APPDATA%\MetrykiPali\projekt.mpali
```

and reopened the next time you start the app. It is plain JSON, and a dated
backup is kept the first time you open the app each day. Saves are written to a
temporary file and then moved into place, so an interrupted save cannot destroy
the journal.

For more than one site, use **Projekt → Zapisz jako...** / **Otwórz...** to keep
separate `.mpali` files. **Projekt → Pokaż folder z danymi** opens the folder.

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

Matches the reference documentation: **12 piles per A4 portrait page**, grouped by
pour day and ordered by date, laid out
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
969 piles**. Exported through Excel the whole schedule on one date gives an
81-page PDF with 12 piles per full page and 9 on the last.

Logging three days (12 / 18 / 12 piles) produces **four** pages — `12.09`,
`13.09` ×2, `14.09` — with no page mixing two dates. The `13.09` page comes out
as piles `11-16, 63-66, 77, 78`, which is page 5 of the reference
`Metryki pali1.pdf` exactly.

## Tests

```powershell
dotnet test
```

96 tests covering the logic behind the UI:

| Area | What is checked |
|---|---|
| `PileNumbersTests` | `1-10, 25, 30-33` parsing, mixed separators, en/em dashes, de-duplication, round-tripping; reversed ranges and junk rejected with the bad fragment named |
| `PileMathTests` | the volume formula, every length in the reference table (6 m → 0.98 … 12 m → 1.96), half-away-from-zero rounding |
| `PileTableReaderTests` | `.csv` / `.xlsx` / `.pdf` inputs, comma *and* dot decimals, headers and notes skipped, a file locked by Excel, unsupported types and empty files |
| `PaginationTests` | days never share a page, an 18-pile day splits 12 + 6, date ordering, configurable page size |
| `MetrykaWriterTests` | the generated workbook read back: label rows, the 48-row block, dates per page, page breaks, A4 fit-to-width, header/footer, borders, an 80-page run |
| `ProjectStoreTests` | journal round-trip, pour dates and corrected lengths preserved, Polish characters, missing/corrupt files, no leftover temp file, daily backup |
| `WorkflowTests` | three site days across two restarts, then one generation; reloading a corrected schedule keeps the journal; moving a pile between days |

Test inputs live in [tests/MetrykiPali.Tests/fixtures/](tests/MetrykiPali.Tests/fixtures/) and double as
sample files you can load into the app by hand:

| File | Shape |
|---|---|
| `tabelka-testowa.xlsx` | 6 ranges / 60 piles, the normal case |
| `tabelka-testowa.pdf` | the same table as a PDF |
| `tabelka-podstawowa.csv` | semicolons, dot decimals, a 7.5 m length |
| `tabelka-przecinki.csv` | semicolons with **comma** decimals, mixed diameters |
| `tabelka-angielska.csv` | commas as field separators |
| `tabelka-smieci.csv` | title lines, a blank row, a hand-written note and a totals row to ignore |

## Project layout

```
src/MetrykiPali/
  Model.cs             PileRange, Pile, WorkDay, ProjectState, volume formula
  PileTableReader.cs   reads .xlsx/.xls/.csv/.pdf, expands ranges into piles
  PileNumbers.cs       parses and formats "1-10, 25, 30-33"
  ProjectStore.cs      saves/restores the journal between runs
  MetrykaWriter.cs     writes the paginated METRYKA PALI workbook
  MainForm.cs          the UI
tests/MetrykiPali.Tests/
  fixtures/            sample schedules in every supported format
publish.ps1            builds the standalone offline .exe
```

## Known limits (it is a POC)

- Output is `.xlsx`; PDF is one *Save as PDF* away in Excel but is not generated
  directly by the app.
- The source table must have its five columns in the order shown above; the app
  does not yet let you map columns by name.
- `Beton z betoniarni` is one value for the whole job, not per day.
- The journal records which piles were poured on a day, not the order within it.
