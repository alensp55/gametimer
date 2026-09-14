namespace GameTime;

internal sealed class SchedulePeriodForm : Form
{
    private readonly CheckBox[] _days = new CheckBox[7];
    private readonly DateTimePicker _start = TimePicker();
    private readonly DateTimePicker _end = TimePicker();
    private readonly CheckBox _allDay = new() { Text = "Весь день", AutoSize = true };

    public SchedulePeriod Period => new()
    {
        Days = Enumerable.Range(0, 7).Where(day => _days[day].Checked).Sum(day => 1 << day),
        StartMinute = _allDay.Checked ? 0 : _start.Value.Hour * 60 + _start.Value.Minute,
        EndMinute = _allDay.Checked ? 0 : _end.Value.Hour * 60 + _end.Value.Minute
    };

    public SchedulePeriodForm(SchedulePeriod period)
    {
        Text = "Интервал принудительного завершения";
        ClientSize = new Size(500, 260);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var days = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        for (int index = 0; index < 7; index++)
        {
            int day = (index + 1) % 7;
            _days[day] = new CheckBox { Text = SchedulePeriod.DayNames[day], AutoSize = true };
            _days[day].Checked = period.Includes((DayOfWeek)day);
            days.Controls.Add(_days[day]);
        }
        layout.Controls.Add(days);
        _start.Value = DateTime.Today.AddMinutes(period.StartMinute);
        _end.Value = DateTime.Today.AddMinutes(period.EndMinute);
        _allDay.Checked = period.StartMinute == period.EndMinute;
        layout.Controls.Add(_allDay);
        var times = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        times.Controls.Add(new Label { Text = "С", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        times.Controls.Add(_start);
        times.Controls.Add(new Label { Text = "до", AutoSize = true, Margin = new Padding(12, 6, 3, 3) });
        times.Controls.Add(_end);
        layout.Controls.Add(times);
        layout.Controls.Add(new Label
        {
            Text = "Если конец раньше начала, интервал заканчивается на следующий день.",
            AutoSize = true, MaximumSize = new Size(410, 0), Margin = new Padding(3, 8, 3, 12)
        });
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var ok = new Button { Text = "ОК", AutoSize = true };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (Period.Days == 0 || (!_allDay.Checked && Period.StartMinute == Period.EndMinute))
            {
                MessageBox.Show(this, UiText.Get("Выберите дни и разные время начала и конца либо «Весь день»."),
                    UiText.Get("Расписание"));
                return;
            }
            DialogResult = DialogResult.OK;
        };
        _allDay.CheckedChanged += (_, _) => _start.Enabled = _end.Enabled = !_allDay.Checked;
        _start.Enabled = _end.Enabled = !_allDay.Checked;
        buttons.Controls.AddRange([ok, cancel]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
        UiText.Apply(this);
    }

    private static DateTimePicker TimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 95
    };
}
