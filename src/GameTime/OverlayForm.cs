namespace GameTime;

internal sealed class OverlayForm : Form
{
    private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 100 };
    private AppSettings _settings = new();
    private double _seconds;
    private long _pulseStart;
    private string _lastGameScreen = "";

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NativeMethods.OverlayStyles;
            return parameters;
        }
    }

    public OverlayForm()
    {
        Text = "GameTime — таймер";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(20, 27, 42);
        ForeColor = Color.FromArgb(231, 238, 249);
        Font = new Font("Segoe UI", 19, FontStyle.Bold);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        _pulseTimer.Tick += (_, _) => Pulse();
    }

    public void Present(AppSettings settings, double seconds, nint gameWindow, bool show)
    {
        _settings = settings;
        _seconds = seconds;
        if (!show)
        {
            _pulseTimer.Stop();
            Hide();
            return;
        }
        if (gameWindow != 0)
            _lastGameScreen = Screen.FromHandle(gameWindow).DeviceName;
        PositionOnScreen();
        if (!Visible)
            Show();
        NativeMethods.KeepOnTop(Handle);
        Opacity = settings.OpacityPercent / 100.0;
        bool pulse = settings.PulseAt100 && TimerDisplay.LimitReached(seconds, settings);
        if (pulse && !_pulseTimer.Enabled)
        {
            _pulseStart = Environment.TickCount64;
            _pulseTimer.Start();
        }
        else if (!pulse)
        {
            _pulseTimer.Stop();
        }
        Invalidate();
    }

    internal void PositionOnScreen()
    {
        Screen[] screens = Screen.AllScreens;
        string name = _settings.Monitor.Length == 0 ? _lastGameScreen : _settings.Monitor;
        Screen screen = screens.FirstOrDefault(item => item.DeviceName == name) ?? Screen.PrimaryScreen ?? screens[0];
        Rectangle bounds = screen.WorkingArea;
        // Move first so PerMonitorV2 applies the destination DPI before measuring the text.
        if (Screen.FromControl(this).DeviceName != screen.DeviceName)
            Location = bounds.Location;
        int padding = (int)(8 * DeviceDpi / 96.0);
        string text = TimerDisplay.Text(_seconds, _settings.LimitMinutes);
        Size textSize = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding);
        ClientSize = new Size(Math.Min(bounds.Width, textSize.Width + padding * 2), textSize.Height + padding * 2);
        Location = CalculateLocation(bounds, Size, _settings, DeviceDpi / 96.0);
    }

    internal static Point CalculateLocation(Rectangle bounds, Size size, AppSettings settings, double scale)
    {
        int x = (int)(settings.OffsetX * scale);
        int y = (int)(settings.OffsetY * scale);
        bool right = settings.Corner is OverlayCorner.TopRight or OverlayCorner.BottomRight;
        bool bottom = settings.Corner is OverlayCorner.BottomLeft or OverlayCorner.BottomRight;
        x = right ? bounds.Right - size.Width - x : bounds.Left + x;
        y = bottom ? bounds.Bottom - size.Height - y : bounds.Top + y;
        x = Math.Clamp(x, bounds.Left, Math.Max(bounds.Left, bounds.Right - size.Width));
        y = Math.Clamp(y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - size.Height));
        return new Point(x, y);
    }

    private void Pulse()
    {
        double phase = (Environment.TickCount64 - _pulseStart) / 4000.0 * Math.PI * 2;
        Opacity = _settings.OpacityPercent / 100.0 * (0.85 + 0.15 * Math.Cos(phase));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int padding = (int)(8 * DeviceDpi / 96.0);
        bool exceeded = TimerDisplay.LimitReached(_seconds, _settings);
        var area = new Rectangle(padding, padding, ClientSize.Width - padding * 2, ClientSize.Height - padding * 2);
        TextRenderer.DrawText(e.Graphics, TimerDisplay.Text(_seconds, _settings.LimitMinutes), Font, area,
            exceeded ? Color.FromArgb(255, 141, 159) : ForeColor, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0021) // WM_MOUSEACTIVATE: never activate, even during a preview.
        {
            message.Result = new nint(3); // MA_NOACTIVATE
            return;
        }
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulseTimer.Dispose();
            Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
