using System.ComponentModel;

namespace GameTime;

internal sealed class DurationInput : FlowLayoutPanel
{
    private readonly IntegerInput _hours = new() { Maximum = 24, Width = 70 };
    private readonly IntegerInput _minutes = new() { Minimum = 1, Maximum = 59, Width = 70 };
    private bool _updating;

    public event Action? ValueChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minutes
    {
        get => (int)(_hours.Value * 60 + _minutes.Value);
        set
        {
            _updating = true;
            _hours.Value = value / 60;
            AdjustRange();
            _minutes.Value = value % 60;
            _updating = false;
            ValueChanged?.Invoke();
        }
    }

    public DurationInput()
    {
        AutoSize = true;
        WrapContents = false;
        Controls.Add(_hours);
        Controls.Add(new Label { Text = ":", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
        Controls.Add(_minutes);
        _hours.ValueChanged += (_, _) =>
        {
            bool updating = _updating;
            _updating = true;
            AdjustRange();
            _updating = updating;
            if (!_updating)
                ValueChanged?.Invoke();
        };
        _minutes.ValueChanged += (_, _) =>
        {
            if (!_updating)
                ValueChanged?.Invoke();
        };
    }

    private void AdjustRange()
    {
        _minutes.Minimum = _hours.Value == 0 ? 1 : 0;
        _minutes.Maximum = _hours.Value == 24 ? 0 : 59;
        _minutes.Enabled = _hours.Value < 24;
    }
}

internal sealed class DailyTargetForm : Form
{
    private readonly CheckBox _enabled = new() { Text = "Ориентир на день", AutoSize = true };
    private readonly DurationInput _duration = new();
    public int Minutes => _enabled.Checked ? _duration.Minutes : 0;

    public DailyTargetForm(int minutes)
    {
        Text = UiText.Get("Ориентир на день");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(420, 225);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _enabled.Checked = minutes > 0;
        _duration.Minutes = minutes > 0 ? minutes : 120;
        _duration.Enabled = _enabled.Checked;
        _enabled.CheckedChanged += (_, _) => _duration.Enabled = _enabled.Checked;
        layout.Controls.Add(_enabled);
        layout.Controls.Add(_duration);
        layout.Controls.Add(new Label
        {
            Text = "Мягкая цель для общего таймера. Приложения не завершаются.", AutoSize = true,
            MaximumSize = new Size(370, 0), Margin = new Padding(3, 8, 3, 12)
        });
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var ok = new Button { Text = "ОК", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([ok, cancel]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
        UiText.Apply(this);
    }
}
