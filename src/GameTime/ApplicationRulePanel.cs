namespace GameTime;

internal sealed class ApplicationRulePanel : UserControl
{
    private readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
    private readonly CheckBox _enabled = new() { Name = "RuleEnabled", Text = "Правила включены", AutoSize = true };
    private readonly ComboBox _tracking = Combo("AppTracking");
    private readonly CheckBox _useLimit = new() { Name = "UseAppLimit", Text = "Дневной лимит", AutoSize = true };
    private readonly DurationInput _limit = new() { Name = "LimitDuration" };
    private readonly ComboBox _action = Combo("LimitAction");
    private readonly ComboBox _schedule = Combo("AppSchedule");
    private readonly Button _editSchedule = new() { Text = "Изменить расписание…", AutoSize = true };
    private readonly Label _periods = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _warning = new()
    {
        Text = "Несохранённые данные приложения могут быть потеряны.", AutoSize = true, ForeColor = Color.DimGray
    };
    private readonly TableLayoutPanel _fields = new() { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1 };
    private AppTimeLimit _rule = new();
    private string? _name;
    private bool _loading;

    public event Action<AppTimeLimit>? Changed;
    public event Action? EditSchedule;

    public ApplicationRulePanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(12, 0, 0, 0);
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(_title);
        Add(_enabled);
        Add(new Label { Text = "Учёт времени", AutoSize = true });
        _tracking.Items.AddRange(["Не считать", "Только активное окно", "Пока приложение запущено"]);
        Add(_tracking);
        Add(_useLimit);
        Add(_limit);
        _action.Items.AddRange(["Предупредить", "Завершить приложение"]);
        Add(_action);
        Add(new Label { Text = "Завершать по расписанию", AutoSize = true });
        _schedule.Items.AddRange(["Выключено", "Общее расписание", "Своё расписание"]);
        Add(_schedule);
        Add(_editSchedule);
        Add(_periods);
        Add(_warning);
        Controls.Add(_fields);
        _enabled.CheckedChanged += (_, _) => OnChange();
        _useLimit.CheckedChanged += (_, _) => OnChange();
        _limit.ValueChanged += OnChange;
        _tracking.SelectedIndexChanged += (_, _) => OnChange();
        _action.SelectedIndexChanged += (_, _) => OnChange();
        _schedule.SelectedIndexChanged += (_, _) => OnChange();
        _editSchedule.Click += (_, _) => EditSchedule?.Invoke();
        LoadRule(null, new AppTimeLimit());
    }

    public void LoadRule(string? name, AppTimeLimit rule)
    {
        _loading = true;
        _name = name;
        _rule = rule.Copy();
        _title.Text = name ?? UiText.Get("Выберите приложение слева");
        _enabled.Checked = rule.Enabled;
        _tracking.SelectedIndex = !rule.Track ? 0 : rule.Mode == TrackingMode.Running ? 2 : 1;
        _useLimit.Checked = rule.HasLimit;
        _limit.Minutes = rule.Minutes;
        _action.SelectedIndex = rule.Terminate ? 1 : 0;
        _schedule.SelectedIndex = (int)rule.ScheduleMode;
        _loading = false;
        UpdateEnabled();
    }

    public void ShowPeriods(IReadOnlyList<SchedulePeriod> periods)
    {
        _periods.Text = _rule.ScheduleMode == AppScheduleMode.None ? "" : periods.Count == 0
            ? UiText.Get("Интервалы не заданы")
            : string.Join("\n", periods.Take(3).Select(ScheduleEditorForm.Describe))
                + (periods.Count > 3 ? "\n…" : "");
    }

    public void ApplyLanguage()
    {
        _loading = true;
        UiText.Apply(this);
        _loading = false;
    }

    private void OnChange()
    {
        if (_loading || _name is null)
            return;
        _loading = true;
        if (_tracking.SelectedIndex == 0)
            _useLimit.Checked = false;
        _rule = _rule with
        {
            Enabled = _enabled.Checked, Track = _tracking.SelectedIndex != 0,
            Mode = _tracking.SelectedIndex == 2 ? TrackingMode.Running : TrackingMode.Foreground,
            UseLimit = _useLimit.Checked, Minutes = _limit.Minutes, Terminate = _action.SelectedIndex == 1,
            ScheduleMode = (AppScheduleMode)_schedule.SelectedIndex
        };
        _loading = false;
        UpdateEnabled();
        Changed?.Invoke(_rule.Copy());
    }

    private void UpdateEnabled()
    {
        foreach (Control control in _fields.Controls)
            if (control != _title)
                control.Visible = _name is not null;
        bool enabled = _enabled.Checked;
        _tracking.Enabled = _schedule.Enabled = enabled;
        _useLimit.Enabled = enabled && _tracking.SelectedIndex != 0;
        _limit.Visible = _action.Visible = _name is not null && _useLimit.Checked;
        _limit.Enabled = _action.Enabled = enabled;
        _editSchedule.Visible = _periods.Visible = _name is not null && _schedule.SelectedIndex != 0;
        _editSchedule.Enabled = enabled;
        _warning.Visible = _name is not null && ((_useLimit.Checked && _action.SelectedIndex == 1)
            || _schedule.SelectedIndex != 0);
    }

    private void Add(Control control)
    {
        if (control is Label label)
            label.MaximumSize = new Size(380, 0);
        int row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(3, 5, 3, 5);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _fields.Controls.Add(control, 0, row);
    }

    private static ComboBox Combo(string name) => new()
    {
        Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top
    };
}
