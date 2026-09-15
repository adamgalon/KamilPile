using System.ComponentModel;
using System.Diagnostics;

namespace MetrykiPali;

public sealed class MainForm : Form
{
    private ProjectState _project = new();
    private BindingList<Pile> _piles = new();
    private readonly BindingList<JournalRow> _journal = new();
    private string _projectPath = ProjectStore.DefaultPath;
    private bool _loading;
    private string? _lastOutput;

    private readonly TextBox _txtSource = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _txtBudowa = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtWykonawca = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtMetoda = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtBetoniarnia = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _numFactor = new()
    {
        DecimalPlaces = 2, Increment = 0.01m, Minimum = 1.00m, Maximum = 3.00m, Dock = DockStyle.Fill
    };
    private readonly NumericUpDown _numPerPage = new() { Minimum = 1, Maximum = 12, Dock = DockStyle.Fill };

    private readonly DateTimePicker _dtDay = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill };
    private readonly TextBox _txtDayPiles = new() { Dock = DockStyle.Fill, PlaceholderText = "np. 1-10, 25, 30-33" };
    private readonly Button _btnAddDay = new() { Text = "Dodaj do dziennika", Dock = DockStyle.Fill };
    private readonly Button _btnAddSelected = new() { Text = "Dodaj zaznaczone z listy pali", Dock = DockStyle.Fill };
    private readonly Button _btnRemoveDay = new() { Text = "Usuń zaznaczony dzień", Dock = DockStyle.Fill };

    private readonly DataGridView _gridJournal = NewGrid();
    private readonly DataGridView _gridRanges = NewGrid();
    private readonly DataGridView _gridPiles = NewGrid();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private readonly Button _btnLoad = new() { Text = "Wczytaj tabelkę...", Dock = DockStyle.Fill, Height = 30 };
    private readonly Button _btnGenerate = new() { Text = "Generuj metryki (.xlsx)", Dock = DockStyle.Fill, Height = 34, Enabled = false };
    private readonly Button _btnOpen = new() { Text = "Otwórz wygenerowany plik", Dock = DockStyle.Fill, Height = 34, Enabled = false };
    private readonly Label _lblStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };

    /// <summary>One row of the journal grid: a day and the piles poured that day.</summary>
    private sealed class JournalRow
    {
        public DateTime Data { get; set; }
        public string Pale { get; set; } = "";
        public int Ilosc { get; set; }
        public double Beton { get; set; }
        public int Strony { get; set; }
    }

    public MainForm()
    {
        Text = "Metryki pali — generator dokumentacji powykonawczej";
        Width = 1240;
        Height = 880;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);

        BuildMenu();
        BuildUi();

        _btnLoad.Click += OnLoadSource;
        _btnGenerate.Click += OnGenerate;
        _btnOpen.Click += (_, _) => OpenLastOutput();
        _btnAddDay.Click += (_, _) => AddToJournal(PileNumbers.Parse(_txtDayPiles.Text));
        _btnAddSelected.Click += OnAddSelected;
        _btnRemoveDay.Click += OnRemoveDay;
        _gridPiles.CellValueChanged += OnPileEdited;
        _numFactor.ValueChanged += (_, _) => { RecalculateConcrete(); AutoSave(); };
        _numPerPage.ValueChanged += (_, _) => { RefreshJournal(); AutoSave(); };
        _txtBetoniarnia.TextChanged += (_, _) => { ApplyPlantToPiles(); AutoSave(); };
        foreach (var box in new[] { _txtBudowa, _txtWykonawca, _txtMetoda })
            box.Leave += (_, _) => AutoSave();
        FormClosing += (_, _) => AutoSave();

        _txtDayPiles.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            _btnAddDay.PerformClick();
        };

        RestoreProject();
    }

    // ------------------------------------------------------------------- UI

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        RowHeadersVisible = false
    };

    private void BuildMenu()
    {
        var file = new ToolStripMenuItem("&Projekt");
        file.DropDownItems.Add("&Nowy", null, (_, _) => NewProject());
        file.DropDownItems.Add("&Otwórz...", null, (_, _) => OpenProject());
        file.DropDownItems.Add("Zapisz &jako...", null, (_, _) => SaveProjectAs());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Pokaż folder z danymi", null, (_, _) =>
            Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_projectPath)!) { UseShellExecute = true }));

        var menu = new MenuStrip();
        menu.Items.Add(file);
        MainMenuStrip = menu;
        Controls.Add(menu);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10, 34, 10, 10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildSourceBox(), 0, 0);
        root.Controls.Add(BuildSettingsBox(), 0, 1);
        root.Controls.Add(BuildJournalBox(), 0, 2);
        root.Controls.Add(BuildTabs(), 0, 3);
        root.Controls.Add(BuildActionBar(), 0, 4);

        Controls.Add(root);
        root.BringToFront();
    }

    private Control BuildSourceBox()
    {
        var box = new GroupBox { Text = "1. Plik wejściowy", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.Controls.Add(_txtSource, 0, 0);
        layout.Controls.Add(_btnLoad, 1, 0);
        box.Controls.Add(layout);
        return box;
    }

    private Control BuildSettingsBox()
    {
        var box = new GroupBox { Text = "2. Nagłówek metryki i obliczenia", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        AddField(layout, 0, "Budowa:", _txtBudowa, "Betoniarnia:", _txtBetoniarnia);
        AddField(layout, 1, "Wykonawca:", _txtWykonawca, "Wsp. betonu:", _numFactor);
        AddField(layout, 2, "Metoda:", _txtMetoda, "Pali na stronę:", _numPerPage);

        box.Controls.Add(layout);
        return box;
    }

    private Control BuildJournalBox()
    {
        var box = new GroupBox { Text = "3. Dziennik robót — wpisz pale wykonane danego dnia", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2, AutoSize = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        layout.Controls.Add(new Label { Text = "Data wykonania:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(_dtDay, 1, 0);
        layout.Controls.Add(new Label { Text = "Pale:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, 0);
        layout.Controls.Add(_txtDayPiles, 3, 0);
        layout.Controls.Add(_btnAddDay, 4, 0);
        layout.Controls.Add(_btnAddSelected, 5, 0);

        var hint = new Label
        {
            Text = "Zakresy i pojedyncze numery, np. \"1-10, 25, 30-33\". Enter zatwierdza. " +
                   "Pal wpisany ponownie z inną datą zostanie przeniesiony.",
            Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, AutoSize = false
        };
        layout.Controls.Add(hint, 3, 1);
        layout.SetColumnSpan(hint, 2);
        layout.Controls.Add(_btnRemoveDay, 5, 1);

        box.Controls.Add(layout);
        return box;
    }

    private Control BuildTabs()
    {
        var tabJournal = new TabPage("Dziennik (dni)");
        tabJournal.Controls.Add(_gridJournal);

        var tabPiles = new TabPage("Pale");
        tabPiles.Controls.Add(_gridPiles);

        var tabRanges = new TabPage("Zakresy z tabelki");
        tabRanges.Controls.Add(_gridRanges);

        _tabs.TabPages.Add(tabJournal);
        _tabs.TabPages.Add(tabPiles);
        _tabs.TabPages.Add(tabRanges);
        return _tabs;
    }

    private Control BuildActionBar()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.Controls.Add(_lblStatus, 0, 0);
        layout.Controls.Add(_btnGenerate, 1, 0);
        layout.Controls.Add(_btnOpen, 2, 0);
        return layout;
    }

    private static void AddField(TableLayoutPanel layout, int row, string leftLabel, Control left, string rightLabel, Control right)
    {
        layout.Controls.Add(new Label { Text = leftLabel, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(left, 1, row);
        layout.Controls.Add(new Label { Text = rightLabel, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, row);
        layout.Controls.Add(right, 3, row);
    }

    // -------------------------------------------------------------- project

    private void RestoreProject()
    {
        ProjectStore.BackupOnce(_projectPath);
        var restored = ProjectStore.Load(_projectPath);
        if (restored is not null) Adopt(restored, _projectPath);
        else ShowProject();
    }

    private void Adopt(ProjectState state, string path)
    {
        _project = state;
        _projectPath = path;
        ShowProject();
    }

    private void ShowProject()
    {
        _loading = true;
        try
        {
            var s = _project.Settings;
            _txtBudowa.Text = s.Budowa;
            _txtWykonawca.Text = s.Wykonawca;
            _txtMetoda.Text = s.Metoda;
            _txtBetoniarnia.Text = s.Betoniarnia;
            _numFactor.Value = ClampDecimal((decimal)s.ConcreteFactor, _numFactor);
            _numPerPage.Value = ClampDecimal(s.PilesPerPage, _numPerPage);
            _dtDay.Value = s.Data == default ? DateTime.Today : s.Data;
            _txtSource.Text = _project.SourcePath ?? "";

            _piles = new BindingList<Pile>(_project.Piles);
            _gridPiles.DataSource = _piles;
            _gridRanges.DataSource = new BindingList<PileRange>(_project.Ranges);
            _gridJournal.DataSource = _journal;
            LocalizeGrids();
        }
        finally
        {
            _loading = false;
        }

        RefreshJournal();
    }

    private static decimal ClampDecimal(decimal value, NumericUpDown control)
        => Math.Clamp(value, control.Minimum, control.Maximum);

    private void CaptureSettings()
    {
        var s = _project.Settings;
        s.Budowa = _txtBudowa.Text.Trim();
        s.Wykonawca = _txtWykonawca.Text.Trim();
        s.Metoda = _txtMetoda.Text.Trim();
        s.Betoniarnia = _txtBetoniarnia.Text.Trim();
        s.ConcreteFactor = (double)_numFactor.Value;
        s.PilesPerPage = (int)_numPerPage.Value;
        s.Data = _dtDay.Value.Date;
    }

    private void AutoSave()
    {
        if (_loading) return;

        CaptureSettings();
        _project.Piles = _piles.ToList();

        try
        {
            ProjectStore.Save(_projectPath, _project);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Nie udało się zapisać projektu: " + ex.Message;
        }
    }

    private void NewProject()
    {
        if (MessageBox.Show(this,
                "Rozpocząć nowy projekt? Bieżący dziennik zostanie wyczyszczony " +
                "(zapisany plik pozostanie na dysku).",
                "Nowy projekt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        Adopt(new ProjectState(), _projectPath);
        _lastOutput = null;
        _btnOpen.Enabled = false;
        AutoSave();
    }

    private void OpenProject()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Otwórz projekt",
            Filter = $"Projekt metryk (*{ProjectStore.FileExtension})|*{ProjectStore.FileExtension}"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var loaded = ProjectStore.Load(dialog.FileName);
        if (loaded is null)
        {
            MessageBox.Show(this, "Nie udało się odczytać tego pliku projektu.", "Błąd",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Adopt(loaded, dialog.FileName);
    }

    private void SaveProjectAs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz projekt jako",
            Filter = $"Projekt metryk (*{ProjectStore.FileExtension})|*{ProjectStore.FileExtension}",
            FileName = "projekt" + ProjectStore.FileExtension
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _projectPath = dialog.FileName;
        AutoSave();
        _lblStatus.Text = "Zapisano projekt: " + _projectPath;
    }

    // --------------------------------------------------------------- source

    private void OnLoadSource(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz tabelkę z palami",
            Filter = "Wszystkie obsługiwane (*.xlsx;*.xls;*.csv;*.pdf)|*.xlsx;*.xls;*.csv;*.pdf|" +
                     "Excel (*.xlsx;*.xls)|*.xlsx;*.xls|CSV (*.csv)|*.csv|PDF (*.pdf)|*.pdf"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            CaptureSettings();

            var ranges = PileTableReader.Read(dialog.FileName).ToList();
            var fresh = PileTableReader.Expand(ranges, _project.Settings);

            // Keep the journal: re-reading a corrected schedule must not discard
            // weeks of recorded pour dates.
            var previous = _piles.ToDictionary(p => p.Number);
            var kept = 0;
            foreach (var pile in fresh)
            {
                if (!previous.TryGetValue(pile.Number, out var old) || old.Executed is null) continue;
                pile.Executed = old.Executed;
                kept++;
            }

            _project.SourcePath = dialog.FileName;
            _project.Ranges = ranges;
            _project.Piles = fresh;

            ShowProject();
            AutoSave();

            _lblStatus.Text = kept > 0
                ? $"Wczytano {ranges.Count} zakresów. Zachowano daty dla {kept} pali."
                : $"Wczytano {ranges.Count} zakresów ({fresh.Count} pali).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Nie udało się wczytać pliku",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // -------------------------------------------------------------- journal

    private void OnAddSelected(object? sender, EventArgs e)
    {
        var numbers = _gridPiles.SelectedCells
            .Cast<DataGridViewCell>()
            .Select(c => c.RowIndex)
            .Distinct()
            .Where(i => i >= 0 && i < _piles.Count)
            .Select(i => _piles[i].Number)
            .ToList();

        if (numbers.Count == 0)
        {
            MessageBox.Show(this, "Zaznacz najpierw pale na zakładce \"Pale\".", "Brak zaznaczenia",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AddToJournal(numbers);
    }

    private void AddToJournal(IReadOnlyCollection<int> requested)
    {
        if (_piles.Count == 0)
        {
            MessageBox.Show(this, "Najpierw wczytaj tabelkę z palami.", "Brak danych",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (requested.Count == 0)
        {
            MessageBox.Show(this, "Podaj numery pali, np. \"1-10, 25, 30-33\".", "Brak numerów",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var date = _dtDay.Value.Date;
        var byNumber = _piles.ToDictionary(p => p.Number);

        var unknown = requested.Where(n => !byNumber.ContainsKey(n)).ToList();
        var moved = requested
            .Where(n => byNumber.TryGetValue(n, out var p) && p.Executed is not null && p.Executed != date)
            .ToList();

        if (unknown.Count > 0 &&
            MessageBox.Show(this,
                $"Tych pali nie ma w tabelce i zostaną pominięte:\n{PileNumbers.Format(unknown)}\n\nKontynuować?",
                "Nieznane numery pali", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (moved.Count > 0 &&
            MessageBox.Show(this,
                $"Te pale mają już inną datę wykonania:\n{PileNumbers.Format(moved)}\n\n" +
                $"Przenieść je na {date:dd.MM.yyyy}?",
                "Pale już w dzienniku", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var added = 0;
        foreach (var number in requested)
        {
            if (!byNumber.TryGetValue(number, out var pile)) continue;
            pile.Executed = date;
            added++;
        }

        _txtDayPiles.Clear();
        _txtDayPiles.Focus();
        RefreshJournal();
        AutoSave();

        _lblStatus.Text = $"Dodano {added} pali do dnia {date:dd.MM.yyyy}." +
                          (moved.Count > 0 ? $" Przeniesiono {moved.Count}." : "") +
                          $"   |   {Summary()}";
    }

    private void OnRemoveDay(object? sender, EventArgs e)
    {
        var row = _gridJournal.CurrentRow?.DataBoundItem as JournalRow;
        if (row is null)
        {
            MessageBox.Show(this, "Zaznacz dzień na liście \"Dziennik (dni)\".", "Brak zaznaczenia",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this,
                $"Usunąć dzień {row.Data:dd.MM.yyyy} ({row.Ilosc} pali)?\n\n" +
                "Pale wrócą na listę nieprzypisanych.",
                "Usuń dzień", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var pile in _piles.Where(p => p.Executed == row.Data))
            pile.Executed = null;

        RefreshJournal();
        AutoSave();
    }

    /// <summary>Rebuilds the day list from the pour dates held on the piles.</summary>
    private void RefreshJournal()
    {
        var perPage = Math.Max(1, (int)_numPerPage.Value);

        _journal.RaiseListChangedEvents = false;
        _journal.Clear();

        foreach (var day in BuildWorkDays())
        {
            _journal.Add(new JournalRow
            {
                Data = day.Date,
                Pale = PileNumbers.Format(day.Piles.Select(p => p.Number)),
                Ilosc = day.Piles.Count,
                Beton = Math.Round(day.Piles.Sum(p => p.Concrete), 2),
                Strony = (int)Math.Ceiling(day.Piles.Count / (double)perPage)
            });
        }

        _journal.RaiseListChangedEvents = true;
        _journal.ResetBindings();

        LocalizeJournalGrid();
        _gridPiles.Refresh();
        _btnGenerate.Enabled = _journal.Count > 0;
        UpdateStatus();
    }

    private List<WorkDay> BuildWorkDays()
        => _piles
            .Where(p => p.Executed is not null)
            .GroupBy(p => p.Executed!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g => new WorkDay { Date = g.Key, Piles = g.OrderBy(p => p.Number).ToList() })
            .ToList();

    // ------------------------------------------------------------ generate

    private async void OnGenerate(object? sender, EventArgs e)
    {
        CaptureSettings();

        var days = BuildWorkDays();
        if (days.Count == 0)
        {
            MessageBox.Show(this, "Dziennik jest pusty — najpierw dodaj pale wykonane w poszczególnych dniach.",
                "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var outstanding = _piles.Count(p => p.Executed is null);
        if (outstanding > 0 &&
            MessageBox.Show(this,
                $"{outstanding} pali nie ma jeszcze przypisanej daty i nie znajdzie się w metrykach.\n\n" +
                "Wygenerować metryki tylko dla pali z dziennika?",
                "Pale bez daty", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz metryki pali",
            Filter = "Skoroszyt Excel (*.xlsx)|*.xlsx",
            FileName = $"Metryki pali {DateTime.Today:yyyy-MM-dd}.xlsx"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var path = dialog.FileName;
        var settings = _project.Settings;

        try
        {
            SetBusy(true, "Generowanie...");
            await Task.Run(() => MetrykaWriter.Write(path, days, settings));

            _lastOutput = path;
            _btnOpen.Enabled = true;

            var pages = MetrykaWriter.Paginate(days, settings.PilesPerPage).Count;
            var piles = days.Sum(d => d.Piles.Count);
            var answer = MessageBox.Show(this,
                $"Zapisano {pages} stron metryk ({piles} pali, {days.Count} dni) do:\n{path}\n\nOtworzyć plik teraz?",
                "Gotowe", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (answer == DialogResult.Yes) OpenLastOutput();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Nie udało się zapisać pliku",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void OpenLastOutput()
    {
        if (_lastOutput is null || !File.Exists(_lastOutput)) return;
        Process.Start(new ProcessStartInfo(_lastOutput) { UseShellExecute = true });
    }

    // ----------------------------------------------------------------- state

    /// <summary>
    /// Keeps the concrete volume in step when the driven length or the diameter
    /// of a single pile is corrected by hand.
    /// </summary>
    private void OnPileEdited(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loading || e.RowIndex < 0 || e.RowIndex >= _piles.Count) return;

        var changed = _gridPiles.Columns[e.ColumnIndex].DataPropertyName;
        if (changed != nameof(Pile.ActualLength) && changed != nameof(Pile.Diameter)) return;

        var pile = _piles[e.RowIndex];
        pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, (double)_numFactor.Value);

        _gridPiles.InvalidateRow(e.RowIndex);
        RefreshJournal();
        AutoSave();
    }

    private void RecalculateConcrete()
    {
        var factor = (double)_numFactor.Value;
        foreach (var pile in _piles)
            pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, factor);

        _gridPiles.Refresh();
        RefreshJournal();
    }

    private void ApplyPlantToPiles()
    {
        var plant = _txtBetoniarnia.Text.Trim();
        foreach (var pile in _piles)
            pile.ConcretePlant = plant;

        _gridPiles.Refresh();
    }

    private void SetBusy(bool busy, string? status)
    {
        _btnLoad.Enabled = !busy;
        _btnGenerate.Enabled = !busy && _journal.Count > 0;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

        if (status is not null) _lblStatus.Text = status;
        else UpdateStatus();
    }

    private string Summary()
    {
        var assigned = _piles.Count(p => p.Executed is not null);
        var pages = _journal.Sum(j => j.Strony);
        return $"Pale: {_piles.Count}   |   W dzienniku: {assigned}   |   Bez daty: {_piles.Count - assigned}   |   " +
               $"Dni: {_journal.Count}   |   Strony: {pages}";
    }

    private void UpdateStatus()
    {
        _lblStatus.Text = _piles.Count == 0
            ? "Wczytaj tabelkę z palami, aby rozpocząć."
            : Summary() + (_lastOutput is null ? "" : $"   |   Zapisano: {Path.GetFileName(_lastOutput)}");
    }

    // ------------------------------------------------------------ grid text

    private void LocalizeGrids()
    {
        SetHeaders(_gridRanges, new()
        {
            [nameof(PileRange.From)] = "Numer od",
            [nameof(PileRange.To)] = "Numer do",
            [nameof(PileRange.Diameter)] = "Średnica [m]",
            [nameof(PileRange.Length)] = "Długość [m]",
            [nameof(PileRange.Reinforcement)] = "Zbrojenie",
            [nameof(PileRange.Count)] = "Ilość pali"
        });

        SetHeaders(_gridPiles, new()
        {
            [nameof(Pile.Number)] = "Numer pala",
            [nameof(Pile.Diameter)] = "Średnica [m]",
            [nameof(Pile.DesignLength)] = "Dł. wg projektu [m]",
            [nameof(Pile.ActualLength)] = "Dł. wykonana [m]",
            [nameof(Pile.Concrete)] = "Beton [m3]",
            [nameof(Pile.ConcretePlant)] = "Betoniarnia",
            [nameof(Pile.Reinforcement)] = "Zbrojenie",
            [nameof(Pile.Executed)] = "Data wykonania"
        });

        if (_gridPiles.Columns[nameof(Pile.Executed)] is { } executed)
        {
            executed.DefaultCellStyle.Format = "dd.MM.yyyy";
            executed.ReadOnly = true;
        }
    }

    private void LocalizeJournalGrid()
    {
        SetHeaders(_gridJournal, new()
        {
            [nameof(JournalRow.Data)] = "Data",
            [nameof(JournalRow.Pale)] = "Pale",
            [nameof(JournalRow.Ilosc)] = "Ilość",
            [nameof(JournalRow.Beton)] = "Beton [m3]",
            [nameof(JournalRow.Strony)] = "Stron"
        });

        if (_gridJournal.Columns[nameof(JournalRow.Data)] is { } date)
        {
            date.DefaultCellStyle.Format = "dd.MM.yyyy";
            date.FillWeight = 40;
        }
        if (_gridJournal.Columns[nameof(JournalRow.Pale)] is { } piles) piles.FillWeight = 200;
        _gridJournal.ReadOnly = true;
    }

    private static void SetHeaders(DataGridView grid, Dictionary<string, string> headers)
    {
        foreach (DataGridViewColumn column in grid.Columns)
            if (headers.TryGetValue(column.DataPropertyName, out var text))
                column.HeaderText = text;
    }
}
