namespace GameTime;

internal sealed class EditTimeForm : Form
{
    private readonly NumericUpDown _hours = new() { Minimum = 0, Maximum = 999, Width = 85 };
    private readonly NumericUpDown _minutes = new() { Minimum = 0, Maximum = 59, Width = 70 };
    private readonly double _originalSeconds;

    internal double Seconds
    {
        get
        {
            double minutes = (double)(_hours.Value * 60 + _minutes.Value);
            return minutes == Math.Floor(_originalSeconds / 60) ? _originalSeconds : minutes * 60;
        }
    }

    public EditTimeForm(string game, double seconds)
    {
        _originalSeconds = seconds;
        Text = "Изменить время — " + game;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(395, 180);
        _hours.Maximum = Math.Max(_hours.Maximum, (decimal)Math.Floor(seconds / 3600));
        _hours.Value = (decimal)Math.Floor(seconds / 3600);
        _minutes.Value = (decimal)Math.Floor(seconds / 60) % 60;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = "Время за сегодня", AutoSize = true, Margin = new Padding(3, 3, 3, 12)
        });
        var time = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
        time.Controls.Add(_hours);
        time.Controls.Add(new Label { Text = "ч", AutoSize = true, Margin = new Padding(3, 6, 12, 3) });
        time.Controls.Add(_minutes);
        time.Controls.Add(new Label { Text = "мин", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        layout.Controls.Add(time, 0, 1);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft
        };
        var save = new Button { Text = "Сохранить", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
