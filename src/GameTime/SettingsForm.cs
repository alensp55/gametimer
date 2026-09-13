using System.Diagnostics;

namespace GameTime;

internal sealed class SettingsForm : Form
{
    private readonly ListBox _games = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _gameName = new()
    {
        PlaceholderText = "Например, Darktide.exe", Anchor = AnchorStyles.Left | AnchorStyles.Right
    };
    private readonly ComboBox _mode = Combo();
    private readonly CheckBox _useLimit = Check("Включить");
    private readonly NumericUpDown _limit = Number(1, 1440);
    private readonly CheckBox _autoStart = Check("Запускать вместе с Windows");
    private readonly CheckBox _pulse = Check("Медленно пульсировать после 100%");
    private readonly CheckBox _overlay = Check("Показывать окно таймера");
    private readonly CheckBox _onlyWithGame = Check("Только при запущенной игре");
    private readonly ComboBox _monitor = Combo();
    private readonly ComboBox _corner = Combo();
    private readonly NumericUpDown _x = Number(0, 10000);
    private readonly NumericUpDown _y = Number(0, 10000);
    private readonly NumericUpDown _opacity = Number(30, 100);
    private readonly Label _today = new() { AutoSize = true, Font = new Font("Segoe UI", 19, FontStyle.Bold) };
    private readonly Label _status = new()
    {
        AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(670, 0)
    };
    private readonly ListView _statistics = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false
    };
    private readonly Button _editTime = new() { Text = "Изменить время…", AutoSize = true, Enabled = false };
    private readonly Button _deleteTime = new() { Text = "Удалить строку", AutoSize = true, Enabled = false };
    private readonly Func<AppSettings, bool> _save;
    private readonly Action<AppSettings> _preview;
    private readonly Action<DateOnly, string, double?> _changeGameTime;
    private DateOnly _statisticsDate;
    private bool _previewing;

    private sealed record MonitorChoice(string Name, string Device)
    {
        public override string ToString() => Name;
    }

    public SettingsForm(AppSettings settings, string dataDirectory, Func<AppSettings, bool> save,
        Action<AppSettings> preview, Action<DateOnly, string, double?> changeGameTime)
    {
        _save = save;
        _preview = preview;
        _changeGameTime = changeGameTime;
        Text = "GameTime — настройки";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(730, 660);
        MinimumSize = new Size(690, 660);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        BackColor = Color.FromArgb(248, 249, 252);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        heading.Controls.Add(_today);
        heading.Controls.Add(_status);
        root.Controls.Add(heading, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        tabs.TabPages.Add(GamesPage());
        tabs.TabPages.Add(OverlayPage());
        tabs.TabPages.Add(StatisticsPage(dataDirectory));
        root.Controls.Add(tabs, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var author = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        author.Controls.Add(new Label { Text = "(c) alensp55@gmail.com", AutoSize = true });
        const string repository = "https://github.com/alensp55/gametimer";
        var link = new LinkLabel { Text = repository, AutoSize = true };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(repository) { UseShellExecute = true });
        author.Controls.Add(link);
        footer.Controls.Add(author, 0, 0);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        actions.Padding = new Padding(0, 8, 0, 0);
        var saveButton = Button("Сохранить и свернуть", (_, _) => SaveSettings(close: true));
        actions.Controls.Add(saveButton);
        actions.Controls.Add(Button("Применить", (_, _) => SaveSettings(close: false)));
        footer.Controls.Add(actions, 1, 0);
        root.Controls.Add(footer, 0, 2);

        _mode.Items.AddRange(["Пока игровой .exe запущен (включая фон)", "Только когда окно игры активно"]);
        _corner.Items.AddRange(["Слева сверху", "Справа сверху", "Слева снизу", "Справа снизу"]);
        _monitor.Items.Add(new MonitorChoice("Монитор игры (авто; иначе основной)", ""));
        foreach (var screen in Screen.AllScreens)
        {
            string suffix = screen.Primary ? " · основной" : "";
            string name = $"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}{suffix}";
            _monitor.Items.Add(new MonitorChoice(name, screen.DeviceName));
        }
        if (settings.Monitor.Length > 0 && !Screen.AllScreens.Any(screen => screen.DeviceName == settings.Monitor))
            _monitor.Items.Add(new MonitorChoice(settings.Monitor + " · сейчас отключён", settings.Monitor));
        _games.Items.AddRange(settings.Games.Cast<object>().ToArray());
        _mode.SelectedIndex = (int)settings.Mode;
        _useLimit.Checked = settings.LimitMinutes > 0;
        _limit.Value = _useLimit.Checked ? settings.LimitMinutes : 120;
        _limit.Enabled = _useLimit.Checked;
        _pulse.Enabled = _useLimit.Checked;
        _autoStart.Checked = settings.AutoStart;
        _pulse.Checked = settings.PulseAt100;
        _overlay.Checked = settings.OverlayEnabled;
        _onlyWithGame.Checked = settings.OverlayOnlyWithGame;
        _onlyWithGame.Enabled = _overlay.Checked;
        _monitor.SelectedIndex = _monitor.Items.Cast<MonitorChoice>().ToList()
            .FindIndex(choice => choice.Device == settings.Monitor);
        _corner.SelectedIndex = (int)settings.Corner;
        _x.Value = settings.OffsetX;
        _y.Value = settings.OffsetY;
        _opacity.Value = settings.OpacityPercent;
        foreach (var number in new[] { _x, _y, _opacity, _limit })
            number.ValueChanged += (_, _) => RefreshPreview();
        _useLimit.CheckedChanged += (_, _) =>
        {
            _limit.Enabled = _useLimit.Checked;
            _pulse.Enabled = _useLimit.Checked;
            RefreshPreview();
        };
        _pulse.CheckedChanged += (_, _) => RefreshPreview();
        _overlay.CheckedChanged += (_, _) => _onlyWithGame.Enabled = _overlay.Checked;
        _monitor.SelectedIndexChanged += (_, _) => RefreshPreview();
        _corner.SelectedIndexChanged += (_, _) => RefreshPreview();
        _gameName.KeyDown += (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            AddGame(_gameName.Text);
        };
    }

    private TabPage GamesPage()
    {
        var page = new TabPage("Игры и время") { Padding = new Padding(14), UseVisualStyleBackColor = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_games, 0, 0);
        var add = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 6, 0, 0)
        };
        add.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int index = 0; index < 3; index++)
            add.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        add.Controls.Add(_gameName, 0, 0);
        add.Controls.Add(Button("Добавить", (_, _) => AddGame(_gameName.Text)), 1, 0);
        add.Controls.Add(Button("Выбрать .exe", (_, _) => BrowseGame()), 2, 0);
        add.Controls.Add(Button("Убрать", (_, _) => RemoveGame()), 3, 0);
        foreach (Control control in add.Controls)
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        layout.Controls.Add(add, 0, 1);

        var fields = Fields();
        Field(fields, "Режим учёта", _mode);
        var limit = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        limit.Controls.Add(_useLimit);
        limit.Controls.Add(_limit);
        limit.Controls.Add(new Label { Text = "минут", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        Field(fields, "Ориентир на день", limit);
        layout.Controls.Add(fields, 0, 2);
        var checks = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        checks.Controls.AddRange([_pulse, _autoStart]);
        layout.Controls.Add(checks, 0, 3);
        layout.Controls.Add(Note("Учёт приблизительный: короткие запуски и переключения могут быть пропущены."), 0, 4);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage OverlayPage()
    {
        var page = new TabPage("Окно таймера") { Padding = new Padding(14), UseVisualStyleBackColor = true };
        var layout = Fields();
        Field(layout, "Отображение", _overlay);
        Field(layout, "Когда показывать", _onlyWithGame);
        Field(layout, "Экран", _monitor);
        Field(layout, "Положение", _corner);
        Field(layout, "Отступ по горизонтали", _x);
        Field(layout, "Отступ по вертикали", _y);
        Field(layout, "Непрозрачность, %", _opacity);
        var preview = Button("Предпросмотр на 15 секунд", (_, _) =>
        {
            _previewing = true;
            RefreshPreview();
        });
        Field(layout, "Проверить расположение", preview);
        var note = Note("В borderless таймер виден поверх игры. Для настоящего exclusive fullscreen выберите " +
            "другой монитор. Окно пропускает клики; положение меняется отступами выше.");
        layout.Controls.Add(note, 0, layout.RowCount);
        layout.SetColumnSpan(note, 2);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage StatisticsPage(string directory)
    {
        var page = new TabPage("Сегодня") { Padding = new Padding(14), UseVisualStyleBackColor = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _statistics.Columns.Add("Игра", 390);
        _statistics.Columns.Add("Время, ч:мин", 170);
        layout.Controls.Add(_statistics, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(0, 4, 0, 0)
        };
        actions.Controls.Add(_editTime);
        actions.Controls.Add(_deleteTime);
        layout.Controls.Add(actions, 0, 1);
        layout.Controls.Add(new TextBox { Text = directory, ReadOnly = true, Dock = DockStyle.Top }, 0, 2);
        _editTime.Click += (_, _) => EditSelectedTime();
        _deleteTime.Click += (_, _) => DeleteSelectedTime();
        _statistics.DoubleClick += (_, _) => EditSelectedTime();
        _statistics.SelectedIndexChanged += (_, _) =>
        {
            _editTime.Enabled = _statistics.SelectedItems.Count == 1;
            _deleteTime.Enabled = _editTime.Enabled;
        };
        _statistics.KeyDown += (_, args) =>
        {
            if (args.KeyCode is not (Keys.Enter or Keys.Delete))
                return;
            args.SuppressKeyPress = true;
            if (args.KeyCode == Keys.Enter)
                EditSelectedTime();
            else
                DeleteSelectedTime();
        };
        page.Controls.Add(layout);
        return page;
    }

    public void UpdateToday(DailyStats today, string status)
    {
        string? selected = _statisticsDate == today.Date && _statistics.SelectedItems.Count == 1
            ? _statistics.SelectedItems[0].Text : null;
        _statisticsDate = today.Date;
        _today.Text = $"Сегодня  {TimerDisplay.Duration(today.TotalSeconds)}";
        _status.Text = status;
        _statistics.BeginUpdate();
        _statistics.Items.Clear();
        foreach (var entry in today.GameSeconds.OrderByDescending(entry => entry.Value))
        {
            var item = new ListViewItem([entry.Key, TimerDisplay.Duration(entry.Value)]) { Tag = entry.Value };
            _statistics.Items.Add(item);
            item.Selected = string.Equals(entry.Key, selected, StringComparison.OrdinalIgnoreCase);
        }
        _statistics.EndUpdate();
    }

    private AppSettings ReadSettings()
    {
        var settings = new AppSettings
        {
            Games = _games.Items.Cast<string>().ToList(), Mode = (TrackingMode)_mode.SelectedIndex,
            LimitMinutes = _useLimit.Checked ? (int)_limit.Value : 0, AutoStart = _autoStart.Checked,
            PulseAt100 = _pulse.Checked, OverlayEnabled = _overlay.Checked,
            OverlayOnlyWithGame = _onlyWithGame.Checked,
            Monitor = ((MonitorChoice)_monitor.SelectedItem!).Device, Corner = (OverlayCorner)_corner.SelectedIndex,
            OffsetX = (int)_x.Value, OffsetY = (int)_y.Value, OpacityPercent = (int)_opacity.Value
        };
        settings.Validate();
        return settings;
    }

    private void SaveSettings(bool close)
    {
        if (_gameName.Text.Trim().Length > 0 && !AddGame(_gameName.Text))
            return;
        if (_save(ReadSettings()) && close)
            Close();
    }

    private void RefreshPreview()
    {
        if (_previewing)
            _preview(ReadSettings());
    }

    private void EditSelectedTime()
    {
        if (_statistics.SelectedItems.Count != 1)
            return;
        var item = _statistics.SelectedItems[0];
        string game = item.Text;
        DateOnly date = _statisticsDate;
        double seconds = (double)item.Tag!;
        using var dialog = new EditTimeForm(game, seconds);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Seconds != seconds)
            _changeGameTime(date, game, dialog.Seconds);
    }

    private void DeleteSelectedTime()
    {
        if (_statistics.SelectedItems.Count != 1)
            return;
        string game = _statistics.SelectedItems[0].Text;
        DateOnly date = _statisticsDate;
        var result = MessageBox.Show(this, $"Удалить строку {game} за сегодня?",
            "Удалить строку", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (result == DialogResult.Yes)
            _changeGameTime(date, game, null);
    }

    private bool AddGame(string name)
    {
        try
        {
            name = AppSettings.NormalizeGame(name);
            if (!_games.Items.Cast<string>().Contains(name, StringComparer.OrdinalIgnoreCase))
                _games.Items.Add(name);
            _gameName.Clear();
            return true;
        }
        catch (InvalidDataException error)
        {
            MessageBox.Show(this, error.Message, "Список игр", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
    }

    private void BrowseGame()
    {
        using var dialog = new OpenFileDialog { Filter = "Приложения (*.exe)|*.exe", Title = "Выберите игру" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddGame(Path.GetFileName(dialog.FileName));
    }

    private void RemoveGame()
    {
        if (_games.SelectedIndex >= 0)
            _games.Items.RemoveAt(_games.SelectedIndex);
    }

    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

    private static NumericUpDown Number(int minimum, int maximum) => new()
    {
        Minimum = minimum, Maximum = maximum, Width = 130, ThousandsSeparator = false
    };

    private static CheckBox Check(string text) => new()
    {
        Text = text, AutoSize = true, Margin = new Padding(3, 6, 3, 6)
    };

    private static Label Note(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = Color.DimGray, Dock = DockStyle.Top,
        Margin = new Padding(3, 8, 3, 3), MaximumSize = new Size(560, 0)
    };

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 3, 6, 3) };
        button.Click += click;
        return button;
    }

    private static TableLayoutPanel Fields()
    {
        var fields = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return fields;
    }

    private static void Field(TableLayoutPanel fields, string text, Control control)
    {
        int row = fields.RowCount++;
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Margin = new Padding(3, 6, 3, 6);
        fields.Controls.Add(control, 1, row);
    }
}
