namespace GameTime;

internal sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _today = new() { Enabled = false };
    private readonly ToolStripMenuItem _pause;
    private readonly ToolStripMenuItem _status = new() { Enabled = false };
    private readonly Icon _appIcon;

    public TrayManager(Action settings, Action pause, Action exit)
    {
        _appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (Icon)SystemIcons.Application.Clone();
        _menu.Items.Add(_today);
        _menu.Items.Add(_status);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Настройки", null, (_, _) => settings());
        _pause = new ToolStripMenuItem("Пауза", null, (_, _) => pause());
        _menu.Items.Add(_pause);
        _menu.Items.Add("Выход", null, (_, _) => exit());
        _icon = new NotifyIcon { Icon = _appIcon, ContextMenuStrip = _menu, Text = "GameTime", Visible = true };
        _icon.DoubleClick += (_, _) => settings();
    }

    public void Update(double seconds, bool paused, string status)
    {
        _today.Text = $"Сегодня: {TimerDisplay.Duration(seconds)}";
        _pause.Text = paused ? "Продолжить мониторинг" : "Пауза";
        _pause.Checked = paused;
        _status.Text = status.Length > 85 ? status[..82] + "…" : status;
        _icon.Text = $"GameTime · {TimerDisplay.Duration(seconds)}" + (paused ? " · Пауза" : "");
    }

    public void Notify(string message)
    {
        _icon.ShowBalloonTip(6000, "GameTime", message, ToolTipIcon.Warning);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
    }
}
