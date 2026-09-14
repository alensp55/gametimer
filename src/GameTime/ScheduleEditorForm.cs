namespace GameTime;

internal sealed class ScheduleEditorForm : Form
{
    private readonly ListView _periods = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false
    };

    public List<SchedulePeriod> Periods => _periods.Items.Cast<ListViewItem>()
        .Select(item => ((SchedulePeriod)item.Tag!) with { }).ToList();

    public ScheduleEditorForm(bool shared, IEnumerable<SchedulePeriod> periods)
    {
        Text = UiText.Get(shared ? "Общее расписание" : "Своё расписание");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 390);
        MinimumSize = new Size(580, 350);
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = shared ? "Завершать в эти часы. Изменения затронут все приложения с общим расписанием."
                : "Завершать это приложение в указанные часы.", AutoSize = true, Dock = DockStyle.Top,
            MaximumSize = new Size(560, 0), Margin = new Padding(3, 3, 3, 12)
        }, 0, 0);
        _periods.Columns.Add("Дни недели", 330);
        _periods.Columns.Add("С", 95);
        _periods.Columns.Add("До", 95);
        foreach (var period in periods)
            AddPeriod(period with { });
        root.Controls.Add(_periods, 0, 1);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        actions.Controls.Add(Button("Добавить интервал", () => Edit(false)));
        actions.Controls.Add(Button("Изменить", () => Edit(true)));
        actions.Controls.Add(Button("Убрать", () =>
        {
            if (_periods.SelectedItems.Count == 1)
                _periods.Items.Remove(_periods.SelectedItems[0]);
        }));
        _periods.DoubleClick += (_, _) => Edit(true);
        root.Controls.Add(actions, 0, 2);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft
        };
        var ok = new Button { Text = "ОК", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([ok, cancel]);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
        UiText.Apply(this);
    }

    public static string Describe(SchedulePeriod period) => period.DayText + " · "
        + (period.StartMinute == period.EndMinute ? UiText.Get("Весь день")
            : SchedulePeriod.FormatTime(period.StartMinute) + "–" + SchedulePeriod.FormatTime(period.EndMinute));

    private void AddPeriod(SchedulePeriod period)
    {
        bool allDay = period.StartMinute == period.EndMinute;
        _periods.Items.Add(new ListViewItem([period.DayText, allDay ? UiText.Get("Весь день")
            : SchedulePeriod.FormatTime(period.StartMinute), allDay ? "" : SchedulePeriod.FormatTime(period.EndMinute)])
        {
            Tag = period
        });
    }

    private void Edit(bool edit)
    {
        var selected = _periods.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
        if (edit && selected is null)
            return;
        using var dialog = new SchedulePeriodForm(edit ? (SchedulePeriod)selected!.Tag! : new SchedulePeriod());
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (edit)
            _periods.Items.Remove(selected!);
        AddPeriod(dialog.Period);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }
}
