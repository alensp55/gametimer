using System.ComponentModel;
using Microsoft.Win32;

namespace GameTime;

internal sealed class GameTimeContext : ApplicationContext
{
    private readonly SessionWindow _dispatcher = new();
    private readonly string _directory;
    private readonly string _settingsPath;
    private readonly TimeStore _store;
    private readonly GameTracker _tracker;
    private readonly OverlayForm _overlay = new();
    private readonly TrayManager _tray;
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _midnight = new();
    private readonly System.Windows.Forms.Timer _previewTimeout = new() { Interval = 15_000 };
    private AppSettings _settings;
    private AppSettings? _previewSettings;
    private SettingsForm? _settingsForm;
    private bool _paused;
    private bool _locked;
    private bool _suspended;
    private bool _exiting;
    private string? _storageError;
    private string? _trackingError;

    private bool Unavailable => _paused || _locked || _suspended;

    public GameTimeContext(string directory, bool startInTray)
    {
        _directory = directory;
        _settingsPath = Path.Combine(directory, "settings.json");
        _settings = AtomicJson.Read(_settingsPath, () => new AppSettings(), value => value.Validate(), out var warning);
        _settings.AutoStart = StartupRegistration.IsEnabled();
        _store = new TimeStore(Path.Combine(directory, "today.json"), DateOnly.FromDateTime(DateTime.Now));
        _tracker = new GameTracker(_store, NativeMethods.ReadClock, GameDetector.Detect);
        _tray = new TrayManager(OpenSettings, TogglePause, RequestExit);
        MainForm = _dispatcher;
        _ = _dispatcher.Handle;
        _dispatcher.ShuttingDown += () => SampleAndSave();
        _dispatcher.SessionEnded += ExitThread;
        _poll.Tick += (_, _) => SampleAndSave();
        _midnight.Tick += (_, _) =>
        {
            SampleAndSave();
            ScheduleMidnight();
        };
        _previewTimeout.Tick += (_, _) => EndPreview();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        SampleAndSave();
        ScheduleMidnight();
        _poll.Start();
        if (warning is not null || _store.RecoveryWarning is not null)
            _tray.Notify(string.Join("\n", new[] { warning, _store.RecoveryWarning }.OfType<string>()));
        if (!startInTray)
            OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, _directory, SaveSettings, Preview, ChangeGameTime);
            _settingsForm.FormClosed += (_, _) =>
            {
                _settingsForm = null;
                EndPreview();
            };
        }
        UpdateViews();
        _settingsForm.Show();
        _settingsForm.WindowState = FormWindowState.Normal;
        _settingsForm.Activate();
    }

    private bool SaveSettings(AppSettings settings)
    {
        try
        {
            settings.Validate();
            AtomicJson.Write(_settingsPath, settings);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(_settingsForm, error.Message, "Не удалось сохранить настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        SampleAndSave();
        bool autoStartChanged = settings.AutoStart != _settings.AutoStart;
        _settings = settings;
        _tracker.Rebase(_settings, Unavailable);
        UpdateViews();
        if (!autoStartChanged)
            return true;
        try
        {
            StartupRegistration.SetEnabled(settings.AutoStart);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            _settings.AutoStart = StartupRegistration.IsEnabled();
            MessageBox.Show(_settingsForm, "Настройки сохранены, но автозапуск изменить не удалось.\n" + error.Message,
                "Автозапуск", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void SampleAndSave()
    {
        try
        {
            _tracker.Poll(_settings, Unavailable);
            _trackingError = null;
        }
        catch (Win32Exception error)
        {
            _trackingError = "Ошибка определения игры: " + error.Message;
            _tracker.Rebase(_settings, paused: true);
        }
        SaveTime();
        UpdateViews();
    }

    private void ChangeGameTime(DateOnly date, string game, double? seconds)
    {
        SampleAndSave();
        try
        {
            _store.SetGameTime(date, game, seconds);
            _storageError = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(_settingsForm, error.Message, "Изменение времени не сохранено",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateViews();
    }

    private bool SaveTime()
    {
        try
        {
            _store.Save();
            _storageError = null;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            string message = "Статистика не сохранена: " + error.Message;
            if (_storageError != message)
                _tray.Notify(message);
            _storageError = message;
            return false;
        }
    }

    private void TogglePause()
    {
        SampleAndSave();
        _paused = !_paused;
        _tracker.Rebase(_settings, Unavailable);
        EndPreview();
    }

    private string Status()
    {
        if (_storageError is not null)
            return _storageError;
        if (_trackingError is not null)
            return _trackingError;
        if (_paused)
            return "Мониторинг на паузе";
        if (_locked || _suspended)
            return "Учёт приостановлен: сессия недоступна";
        if (_settings.Games.Count == 0)
            return "Добавьте игровые .exe в список ниже";
        if (_tracker.Current.Warning is not null)
            return _tracker.Current.Warning;
        if (_settings.Monitor.Length > 0 && !Screen.AllScreens.Any(s => s.DeviceName == _settings.Monitor))
            return "Выбранный монитор отключён; таймер перенесён на основной";
        if (_tracker.Current.Games.Count == 0)
            return "Ожидание игры · следующая проверка в течение минуты";
        return "Учитываются: " + string.Join(", ", _tracker.Current.Games);
    }

    private void UpdateViews()
    {
        string status = Status();
        _tray.Update(_store.Today.TotalSeconds, _paused, status);
        _settingsForm?.UpdateToday(_store.Today, status);
        bool preview = _previewSettings is not null && !_locked && !_suspended;
        bool show = !Unavailable && _settings.OverlayEnabled
            && (!_settings.OverlayOnlyWithGame || _tracker.Current.HasRunningGame);
        _overlay.Present(_previewSettings ?? _settings, _store.Today.TotalSeconds,
            _tracker.Current.GameWindow, show || preview);
    }

    private void Preview(AppSettings settings)
    {
        _previewSettings = settings;
        _previewTimeout.Stop();
        _previewTimeout.Start();
        UpdateViews();
    }

    private void EndPreview()
    {
        _previewTimeout.Stop();
        _previewSettings = null;
        UpdateViews();
    }

    private void ScheduleMidnight()
    {
        DateTime now = DateTime.Now;
        _midnight.Interval = Math.Max(100, (int)(now.Date.AddDays(1) - now).TotalMilliseconds + 50);
        _midnight.Start();
    }

    private void Post(Action action)
    {
        if (!_exiting && !_dispatcher.IsDisposed)
            _dispatcher.BeginInvoke(action);
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
    {
        if (args.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock
            or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.RemoteConnect)
        {
            Post(() =>
            {
                SampleAndSave();
                _locked = args.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect
                    or SessionSwitchReason.RemoteDisconnect;
                _tracker.Rebase(_settings, Unavailable);
                EndPreview();
            });
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode is not (PowerModes.Suspend or PowerModes.Resume))
            return;
        Post(() =>
        {
            SampleAndSave();
            _suspended = args.Mode == PowerModes.Suspend;
            _tracker.Rebase(_settings, Unavailable);
            ScheduleMidnight();
            EndPreview();
        });
    }

    private void OnTimeChanged(object? sender, EventArgs args)
    {
        Post(() =>
        {
            // Drop the interval straddling a wall-clock discontinuity; never manufacture elapsed game time.
            _tracker.Rebase(_settings, Unavailable);
            SaveTime();
            ScheduleMidnight();
            UpdateViews();
        });
    }

    private void OnDisplaysChanged(object? sender, EventArgs args) => Post(UpdateViews);

    private void RequestExit()
    {
        SampleAndSave();
        if (_storageError is not null)
        {
            var result = MessageBox.Show(_storageError + "\nВыйти с потерей несохранённого времени?", "GameTime",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
                return;
        }
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _exiting = true;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
            _poll.Dispose();
            _midnight.Dispose();
            _previewTimeout.Dispose();
            _settingsForm?.Dispose();
            _overlay.Dispose();
            _tray.Dispose();
            _dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class SessionWindow : Form
    {
        public event Action? ShuttingDown;
        public event Action? SessionEnded;

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0011) // WM_QUERYENDSESSION
            {
                ShuttingDown?.Invoke();
                message.Result = new nint(1);
                return;
            }
            if (message.Msg == 0x0016 && message.WParam != 0) // WM_ENDSESSION
            {
                SessionEnded?.Invoke();
                return;
            }
            base.WndProc(ref message);
        }
    }
}
