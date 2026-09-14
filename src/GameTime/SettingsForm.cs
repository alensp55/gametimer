using System.Diagnostics;

namespace GameTime;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _draft;
    private readonly ApplicationRulePanel _editor = new();
    private string? _selected;
    private bool _refreshing;
    private bool _localizing;
    private readonly NumericUpDown _pollSeconds = Number(1, 3600);
    private readonly TextBox _gameName = new()
    {
        Name = "GameName", PlaceholderText = "Например, Darktide.exe",
        Anchor = AnchorStyles.Left | AnchorStyles.Right
    };
    private readonly ComboBox _language = Combo();
    private readonly LinkLabel _support = new() { AutoSize = true };
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
        Name = "Applications", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, ShowItemToolTips = true, CheckBoxes = true
    };
    private readonly Button _editTime = new() { Text = "Изменить время…", AutoSize = true, Enabled = false };
    private readonly Button _remove = new() { Text = "Удалить", AutoSize = true, Enabled = false };
    private readonly Func<AppSettings, bool> _save;
    private readonly Action<AppSettings> _preview;
    private readonly Action<DateOnly, string, double?> _changeGameTime;
    private readonly Func<DateOnly, string, bool>? _removeGame;
    private Dictionary<string, double> _seconds = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly _statisticsDate;
    private double _totalSeconds;
    private bool _previewing;

    private sealed record MonitorChoice(string Name, string Device)
    {
        public override string ToString() => Name;
    }

    public SettingsForm(AppSettings settings, string dataDirectory, Func<AppSettings, bool> save,
        Action<AppSettings> preview, Action<DateOnly, string, double?> changeGameTime,
        Func<DateOnly, string, bool>? removeGame = null)
    {
        UiText.Language = settings.Language;
        _save = save;
        _preview = preview;
        _changeGameTime = changeGameTime;
        _removeGame = removeGame;
        _draft = settings.Copy();
        _draft.Validate();
        Text = "OneMoreTimer — настройки";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(1080, 710);
        MinimumSize = new Size(1020, 710);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        BackColor = Color.FromArgb(248, 249, 252);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        heading.Controls.Add(_today, 0, 0);
        heading.Controls.Add(Button("Изменить ориентир…", (_, _) => EditDailyTarget()), 1, 0);
        heading.Controls.Add(_status, 0, 1);
        heading.SetColumnSpan(_status, 2);
        root.Controls.Add(heading, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        tabs.TabPages.Add(GamesPage());
        tabs.TabPages.Add(OverlayPage());


        tabs.TabPages.Add(GeneralPage());
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
        const string repository = "https://github.com/alensp55/OneMoreTimer";
        var link = new LinkLabel { Text = repository, AutoSize = true };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(repository) { UseShellExecute = true });
        author.Controls.Add(link);
        _support.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(UiText.SupportUrl)
        {
            UseShellExecute = true
        });
        author.Controls.Add(_support);
        footer.Controls.Add(author, 0, 0);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        actions.Padding = new Padding(0, 8, 0, 0);
        var saveButton = Button("ОК", (_, _) => SaveSettings(close: true));
        actions.Controls.Add(saveButton);
        actions.Controls.Add(Button("Применить", (_, _) => SaveSettings(close: false)));
        footer.Controls.Add(actions, 1, 0);
        root.Controls.Add(footer, 0, 2);

        _language.Name = "Language";
        _language.Items.AddRange(["English", "Русский"]);
        _language.SelectedIndex = (int)settings.Language;
        _corner.Items.AddRange(["Слева сверху", "Справа сверху", "Слева снизу", "Справа снизу"]);
        FillMonitors(settings.Monitor);
        _pollSeconds.Value = settings.PollIntervalSeconds;
        _pulse.Enabled = settings.LimitMinutes > 0;

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
        foreach (var number in new[] { _x, _y, _opacity })
            number.ValueChanged += (_, _) => RefreshPreview();


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
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        bool previewing = _previewing;
        _previewing = false;
        _localizing = true;
        UiText.Apply(this);
        FillMonitors((_monitor.SelectedItem as MonitorChoice)?.Device ?? "");
        _support.Text = UiText.Get("Поддержать разработчика");
        _localizing = false;
        RefreshApplications();
        LoadSelection();
        UpdateHeading();
        _previewing = previewing;
    }

    private void UpdateHeading()
    {
        _today.Text = UiText.Get("Сегодня  ") + TimerDisplay.Duration(_totalSeconds);
        if (_draft.LimitMinutes > 0)
            _today.Text += " / " + TimerDisplay.Duration(_draft.LimitMinutes * 60);
    }

    private void EditDailyTarget()
    {
        using var dialog = new DailyTargetForm(_draft.LimitMinutes);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        _draft.LimitMinutes = dialog.Minutes;
        _pulse.Enabled = dialog.Minutes > 0;
        UpdateHeading();
        RefreshPreview();
    }

    private void FillMonitors(string selected)
    {
        _monitor.Items.Clear();
        _monitor.Items.Add(new MonitorChoice(UiText.Get("Монитор игры (авто; иначе основной)"), ""));
        foreach (var screen in Screen.AllScreens)
        {
            string suffix = screen.Primary ? " · " + UiText.Get("основной") : "";
            string name = $"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}{suffix}";
            _monitor.Items.Add(new MonitorChoice(name, screen.DeviceName));
        }
        if (selected.Length > 0 && !Screen.AllScreens.Any(screen => screen.DeviceName == selected))
            _monitor.Items.Add(new MonitorChoice(selected + " · " + UiText.Get("сейчас отключён"), selected));
        _monitor.SelectedIndex = _monitor.Items.Cast<MonitorChoice>().ToList()
            .FindIndex(choice => choice.Device == selected);
    }

    private TabPage GamesPage()
    {
        var page = new TabPage("Приложения") { Padding = new Padding(12), UseVisualStyleBackColor = true };
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        var listArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        listArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        listArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _statistics.Columns.Add("Приложение", 185);
        _statistics.Columns.Add("Сегодня, ч:мин", 120);
        _statistics.Columns.Add("Правила", 230);
        _statistics.Resize += (_, _) =>
        {
            int used = _statistics.Columns[0].Width + _statistics.Columns[1].Width;
            _statistics.Columns[2].Width = Math.Max(120, _statistics.ClientSize.Width - used - 5);
        };
        listArea.Controls.Add(_statistics, 0, 0);
        var add = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 1 };
        add.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        add.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        add.Controls.Add(_gameName, 0, 0);
        add.Controls.Add(Button("Добавить", (_, _) => AddGame(_gameName.Text)), 1, 0);
        add.Controls.Add(Button("Выбрать .exe", (_, _) => BrowseGame()), 2, 0);
        foreach (Control control in add.Controls)
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        listArea.Controls.Add(add, 0, 1);
        columns.Controls.Add(listArea, 0, 0);

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.Controls.Add(_editor, 0, 0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        actions.Controls.AddRange([_editTime, _remove]);
        details.Controls.Add(actions, 0, 1);
        columns.Controls.Add(details, 1, 0);
        _editTime.Click += (_, _) => EditSelectedTime();
        _remove.Click += (_, _) => RemoveGame();
        _statistics.SelectedIndexChanged += (_, _) =>
        {
            if (_refreshing || _statistics.SelectedItems.Count != 1)
                return;
            _selected = _statistics.SelectedItems[0].Text;
            LoadSelection();
        };
        _statistics.ItemChecked += (_, args) =>
        {
            if (_refreshing)
                return;
            string name = args.Item.Text;
            var previous = RuleFor(name);
            if (previous.Enabled == args.Item.Checked)
                return;
            var rule = previous with { Enabled = args.Item.Checked };
            if (!_draft.Games.Contains(name, StringComparer.OrdinalIgnoreCase))
                _draft.Games.Add(name);
            _draft.AppLimits[name] = rule;
            args.Item.SubItems[2].Text = DescribeRule(rule);
            if (_selected == name)
                LoadSelection();
        };
        _editor.Changed += rule =>
        {
            if (_localizing || _selected is null)
                return;
            StoreRule(_selected, rule);
        };
        _editor.EditSchedule += EditSchedule;
        page.Controls.Add(columns);
        return page;
    }

    private AppTimeLimit RuleFor(string name)
    {
        if (_draft.AppLimits.TryGetValue(name, out var rule))
            return rule;
        return new AppTimeLimit { Enabled = false, Track = false, Mode = TrackingMode.Foreground, UseLimit = false };
    }

    private void StoreRule(string name, AppTimeLimit rule)
    {
        if (!_draft.Games.Contains(name, StringComparer.OrdinalIgnoreCase))
            _draft.Games.Add(name);
        _draft.AppLimits[name] = rule;
        RefreshApplications(name);
        _editor.ShowPeriods(_draft.PeriodsFor(rule));
    }

    private void LoadSelection()
    {
        _editTime.Enabled = _remove.Enabled = _selected is not null;
        var rule = _selected is null ? new AppTimeLimit() : RuleFor(_selected);
        _editor.LoadRule(_selected, rule);
        _editor.ShowPeriods(_draft.PeriodsFor(rule));
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
        Field(layout, "После общего ориентира", _pulse);
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

    private TabPage GeneralPage()
    {
        var page = new TabPage("Общие настройки") { Padding = new Padding(14), UseVisualStyleBackColor = true };
        var fields = Fields();
        Field(fields, "Язык / Language", _language);
        Field(fields, "Интервал опроса, сек.", _pollSeconds);
        Field(fields, "Автозапуск", _autoStart);
        var note = Note("Интервал общий для учёта времени и завершения приложений. Язык меняется после «Применить».");
        fields.Controls.Add(note, 0, fields.RowCount);
        fields.SetColumnSpan(note, 2);
        page.Controls.Add(fields);
        return page;
    }

    public void UpdateToday(DailyStats today, string status)
    {
        _statisticsDate = today.Date;
        _totalSeconds = today.TotalSeconds;
        _seconds = new Dictionary<string, double>(today.GameSeconds, StringComparer.OrdinalIgnoreCase);
        UpdateHeading();
        _status.Text = status;
        RefreshApplications();
    }

    private void RefreshApplications(string? selected = null)
    {
        selected ??= _selected;
        var names = _draft.Games.Union(_seconds.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
        if (selected is null || !names.Contains(selected, StringComparer.OrdinalIgnoreCase))
            selected = names.FirstOrDefault();
        bool selectionChanged = _selected != selected;
        _selected = selected;
        _refreshing = true;
        _statistics.BeginUpdate();
        _statistics.Items.Clear();
        foreach (string name in names)
        {
            var rule = RuleFor(name);
            double seconds = _seconds.GetValueOrDefault(name);
            string time = seconds > 0 || rule.Track ? TimerDisplay.Duration(seconds) : "—";
            string summary = DescribeRule(rule);
            var item = new ListViewItem([name, time, summary])
            {
                Tag = seconds, Checked = rule.Enabled, ToolTipText = summary
            };
            _statistics.Items.Add(item);
            item.Selected = string.Equals(name, selected, StringComparison.OrdinalIgnoreCase);
        }
        _statistics.EndUpdate();
        _refreshing = false;
        if (selectionChanged)
            LoadSelection();
    }

    private string DescribeRule(AppTimeLimit rule)
    {
        if (!rule.Enabled)
            return UiText.Get("Приостановлено");
        var parts = new List<string>();
        if (rule.Track)
            parts.Add(UiText.Get(rule.Mode == TrackingMode.Running ? "Пока запущено" : "Активное окно"));
        if (rule.HasLimit)
            parts.Add(TimerDisplay.Duration(rule.Minutes * 60) + " · "
                + UiText.Get(rule.Terminate ? "Завершить" : "Предупредить"));
        if (rule.ScheduleMode != AppScheduleMode.None)
        {
            var periods = _draft.PeriodsFor(rule);
            parts.Add(periods.Count == 0 ? UiText.Get("Интервалы не заданы")
                : UiText.Get("Завершать: ") + ScheduleEditorForm.Describe(periods[0])
                    + (periods.Count > 1 ? " …" : ""));
        }
        return parts.Count == 0 ? UiText.Get("Нет действий") : string.Join(" · ", parts);
    }

    private AppSettings ReadSettings()
    {
        var settings = _draft.Copy();
        settings.Language = (AppLanguage)_language.SelectedIndex;
        settings.PollIntervalSeconds = (int)_pollSeconds.Value;
        settings.AutoStart = _autoStart.Checked;
        settings.PulseAt100 = _pulse.Checked;
        settings.OverlayEnabled = _overlay.Checked;
        settings.OverlayOnlyWithGame = _onlyWithGame.Checked;
        settings.Monitor = ((MonitorChoice)_monitor.SelectedItem!).Device;
        settings.Corner = (OverlayCorner)_corner.SelectedIndex;
        settings.OffsetX = (int)_x.Value;
        settings.OffsetY = (int)_y.Value;
        settings.OpacityPercent = (int)_opacity.Value;
        settings.Validate();
        return settings;
    }

    private void SaveSettings(bool close)
    {
        if (_gameName.Text.Trim().Length > 0 && !AddGame(_gameName.Text))
            return;
        try
        {
            var settings = ReadSettings();
            if (!_save(settings))
                return;
            UiText.Language = settings.Language;
            ApplyLanguage();
            if (close)
                Close();
        }
        catch (InvalidDataException error)
        {
            MessageBox.Show(this, error.Message, UiText.Get("Настройки"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
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

    private void EditSchedule()
    {
        if (_selected is null)
            return;
        string selected = _selected;
        var rule = RuleFor(selected);
        bool shared = rule.ScheduleMode == AppScheduleMode.Shared;
        using var dialog = new ScheduleEditorForm(shared, _draft.PeriodsFor(rule));
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (shared)
            _draft.Schedule.Periods = dialog.Periods;
        else
            _draft.AppLimits[selected] = rule with { CustomPeriods = dialog.Periods };
        LoadSelection();
        RefreshApplications();
    }

    private bool AddGame(string name)
    {
        try
        {
            name = AppSettings.NormalizeGame(name);
            if (!_draft.Games.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _draft.Games.Add(name);
                _draft.AppLimits[name] = new AppTimeLimit { Mode = TrackingMode.Foreground, UseLimit = false };
            }
            _gameName.Clear();
            RefreshApplications(name);
            return true;
        }
        catch (InvalidDataException error)
        {
            MessageBox.Show(this, error.Message, UiText.Get("Список игр"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
    }

    private void BrowseGame()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = UiText.Get("Приложения (*.exe)|*.exe"), Title = UiText.Get("Выберите игру")
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddGame(Path.GetFileName(dialog.FileName));
    }

    private void RemoveGame()
    {
        if (_statistics.SelectedItems.Count != 1)
            return;
        string game = _statistics.SelectedItems[0].Text;
        string message = UiText.Format("Удалить {0} из учёта и статистики за сегодня?\n" +
            "Общий итог будет пересчитан. Удаление сохраняется сразу.", game);
        var result = MessageBox.Show(this, message, UiText.Get("Удалить приложение"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
            return;
        if (_removeGame is not null && !_removeGame(_statisticsDate, game))
            return;
        if (_removeGame is null && _seconds.ContainsKey(game))
            _changeGameTime(_statisticsDate, game, null);
        _draft.Games.RemoveAll(name => name.Equals(game, StringComparison.OrdinalIgnoreCase));
        _draft.AppLimits.Remove(game);
        _seconds.Remove(game);
        RefreshApplications();
    }

    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

    private static IntegerInput Number(int minimum, int maximum) => new()
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
