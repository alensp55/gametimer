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
        _icon = new NotifyIcon { Icon = _appIcon, ContextMenuStrip = _menu, Text = "OneMoreTimer", Visible = true };
        _icon.DoubleClick += (_, _) => settings();
    }

    public void Update(double seconds, bool paused, string status)
    {
        _today.Text = UiText.Get("Сегодня: ") + TimerDisplay.Duration(seconds);
        _menu.Items[3].Text = UiText.Get("Настройки");
        _menu.Items[5].Text = UiText.Get("Выход");
        _pause.Text = UiText.Get(paused ? "Продолжить" : "Пауза учёта и завершения");
        _pause.Checked = paused;
        _status.Text = status.Length > 85 ? status[..82] + "…" : status;
        _icon.Text = $"OneMoreTimer · {TimerDisplay.Duration(seconds)}" + (paused ? UiText.Get(" · Пауза") : "");
    }

    public void Notify(string message)
    {
        _icon.ShowBalloonTip(6000, "OneMoreTimer", message, ToolTipIcon.Warning);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _appIcon.Dispose();
    }
}
