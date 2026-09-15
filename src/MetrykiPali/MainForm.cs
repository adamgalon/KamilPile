using System.ComponentModel;
using System.Diagnostics;

namespace MetrykiPali;

public sealed class MainForm : Form
{
    private readonly MetrykaSettings _settings = new();
    private List<PileRange> _ranges = new();
    private BindingList<Pile> _piles = new();

    private readonly TextBox _txtSource = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _txtBudowa = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtWykonawca = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtMetoda = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtBetoniarnia = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dtData = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Fill };
    private readonly NumericUpDown _numFactor = new()
    {
        DecimalPlaces = 2,
        Increment = 0.01m,
        Minimum = 1.00m,
        Maximum = 3.00m,
        Dock = DockStyle.Fill
    };
    private readonly NumericUpDown _numPerPage = new()
    {
        Minimum = 1,
        Maximum = 12,
        Dock = DockStyle.Fill
    };

    private readonly DataGridView _gridRanges = NewGrid();
    private readonly DataGridView _gridPiles = NewGrid();
    private readonly Button _btnLoad = new() { Text = "Wczytaj tabelkę...", Dock = DockStyle.Fill, Height = 30 };
    private readonly Button _btnGenerate = new() { Text = "Generuj metryki (.xlsx)", Dock = DockStyle.Fill, Height = 34, Enabled = false };
    private readonly Button _btnOpen = new() { Text = "Otwórz wygenerowany plik", Dock = DockStyle.Fill, Height = 34, Enabled = false };
    private readonly Label _lblStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };

    private string? _lastOutput;

    public MainForm()
    {
        Text = "Metryki pali — generator dokumentacji powykonawczej";
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 640);

        BuildUi();
        LoadSettingsIntoUi();

        _btnLoad.Click += OnLoad;
        _btnGenerate.Click += OnGenerate;
        _btnOpen.Click += (_, _) => OpenLastOutput();
        _numFactor.ValueChanged += (_, _) => RecalculateConcrete();
        _numPerPage.ValueChanged += (_, _) => UpdateStatus();
        _txtBetoniarnia.TextChanged += (_, _) => ApplyPlantToPiles();
        _gridPiles.CellValueChanged += OnPileEdited;
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

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildSourceBox(), 0, 0);
        root.Controls.Add(BuildSettingsBox(), 0, 1);
        root.Controls.Add(BuildTabs(), 0, 2);
        root.Controls.Add(BuildActionBar(), 0, 3);

        Controls.Add(root);
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

        AddField(layout, 0, "Budowa:", _txtBudowa, "Data:", _dtData);
        AddField(layout, 1, "Wykonawca:", _txtWykonawca, "Betoniarnia:", _txtBetoniarnia);
        AddField(layout, 2, "Metoda:", _txtMetoda, "Wsp. betonu:", _numFactor);
        AddField(layout, 3, "", new Label { Text = "Objętość betonu = π/4 · D² · L · wsp.   (1,30 odtwarza wartości z dokumentacji wzorcowej)", Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, AutoSize = false }, "Pali na stronę:", _numPerPage);

        box.Controls.Add(layout);
        return box;
    }

    private static void AddField(TableLayoutPanel layout, int row, string leftLabel, Control left, string rightLabel, Control right)
    {
        layout.Controls.Add(new Label { Text = leftLabel, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(left, 1, row);
        layout.Controls.Add(new Label { Text = rightLabel, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 2, row);
        layout.Controls.Add(right, 3, row);
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabPiles = new TabPage("3. Pale (podgląd — można edytować)");
        tabPiles.Controls.Add(_gridPiles);

        var tabRanges = new TabPage("Zakresy z tabelki");
        tabRanges.Controls.Add(_gridRanges);

        tabs.TabPages.Add(tabPiles);
        tabs.TabPages.Add(tabRanges);
        return tabs;
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

    private void LoadSettingsIntoUi()
    {
        _txtBudowa.Text = _settings.Budowa;
        _txtWykonawca.Text = _settings.Wykonawca;
        _txtMetoda.Text = _settings.Metoda;
        _txtBetoniarnia.Text = _settings.Betoniarnia;
        _dtData.Value = _settings.Data;
        _numFactor.Value = (decimal)_settings.ConcreteFactor;
        _numPerPage.Value = _settings.PilesPerPage;
        UpdateStatus();
    }

    private void ReadSettingsFromUi()
    {
        _settings.Budowa = _txtBudowa.Text.Trim();
        _settings.Wykonawca = _txtWykonawca.Text.Trim();
        _settings.Metoda = _txtMetoda.Text.Trim();
        _settings.Betoniarnia = _txtBetoniarnia.Text.Trim();
        _settings.Data = _dtData.Value.Date;
        _settings.ConcreteFactor = (double)_numFactor.Value;
        _settings.PilesPerPage = (int)_numPerPage.Value;
    }

    // --------------------------------------------------------------- actions

    private void OnLoad(object? sender, EventArgs e)
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
            ReadSettingsFromUi();

            _ranges = PileTableReader.Read(dialog.FileName).ToList();
            _piles = new BindingList<Pile>(PileTableReader.Expand(_ranges, _settings));

            _txtSource.Text = dialog.FileName;
            _gridRanges.DataSource = new BindingList<PileRange>(_ranges);
            _gridPiles.DataSource = _piles;
            LocalizeGrids();

            _btnGenerate.Enabled = _piles.Count > 0;
            UpdateStatus();
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

    private async void OnGenerate(object? sender, EventArgs e)
    {
        ReadSettingsFromUi();

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz metryki pali",
            Filter = "Skoroszyt Excel (*.xlsx)|*.xlsx",
            FileName = $"Metryki pali {_settings.Data:yyyy-MM-dd}.xlsx"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var path = dialog.FileName;
        var piles = _piles.ToList();
        var settings = _settings;

        try
        {
            SetBusy(true, "Generowanie...");
            await Task.Run(() => MetrykaWriter.Write(path, piles, settings));

            _lastOutput = path;
            _btnOpen.Enabled = true;
            UpdateStatus();

            var pages = (int)Math.Ceiling(piles.Count / (double)settings.PilesPerPage);
            var answer = MessageBox.Show(this,
                $"Zapisano {pages} stron metryk ({piles.Count} pali) do:\n{path}\n\nOtworzyć plik teraz?",
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
        if (e.RowIndex < 0 || e.RowIndex >= _piles.Count) return;

        var changed = _gridPiles.Columns[e.ColumnIndex].DataPropertyName;
        if (changed != nameof(Pile.ActualLength) && changed != nameof(Pile.Diameter)) return;

        var pile = _piles[e.RowIndex];
        pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, (double)_numFactor.Value);

        _gridPiles.InvalidateRow(e.RowIndex);
        UpdateStatus();
    }

    private void RecalculateConcrete()
    {
        var factor = (double)_numFactor.Value;
        foreach (var pile in _piles)
            pile.Concrete = PileMath.Concrete(pile.Diameter, pile.ActualLength, factor);

        _gridPiles.Refresh();
        UpdateStatus();
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
        _btnGenerate.Enabled = !busy && _piles.Count > 0;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

        if (status is not null) _lblStatus.Text = status;
        else UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_piles.Count == 0)
        {
            _lblStatus.Text = "Wczytaj tabelkę z palami, aby rozpocząć.";
            return;
        }

        var perPage = Math.Max(1, (int)_numPerPage.Value);
        var pages = (int)Math.Ceiling(_piles.Count / (double)perPage);
        var concrete = _piles.Sum(p => p.Concrete);

        _lblStatus.Text =
            $"Zakresy: {_ranges.Count}   |   Pale: {_piles.Count}   |   Strony: {pages}   |   " +
            $"Beton razem: {concrete:N2} m³" +
            (_lastOutput is null ? "" : $"   |   Zapisano: {Path.GetFileName(_lastOutput)}");
    }

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
            [nameof(Pile.Reinforcement)] = "Zbrojenie"
        });
    }

    private static void SetHeaders(DataGridView grid, Dictionary<string, string> headers)
    {
        foreach (DataGridViewColumn column in grid.Columns)
            if (headers.TryGetValue(column.DataPropertyName, out var text))
                column.HeaderText = text;
    }
}
