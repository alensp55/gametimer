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
    private readonly ProcessEnforcer _enforcer = new();
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
    private string? _terminationError;
    private long _lastSaveTick;
    private DateOnly _noticeDate;
    private HashSet<string> _notifiedLimits = new(StringComparer.OrdinalIgnoreCase);

    private bool Unavailable => _paused || _locked || _suspended;

    public GameTimeContext(string directory, bool startInTray)
    {
        _directory = directory;
        _settingsPath = Path.Combine(directory, "settings.json");
        _settings = AtomicJson.Read(_settingsPath, () => new AppSettings(), value => value.Validate(), out var warning);
        UiText.Language = _settings.Language;
        _settings.AutoStart = StartupRegistration.IsEnabled();
        _poll.Interval = _settings.PollIntervalSeconds * 1000;
        _store = new TimeStore(Path.Combine(directory, "today.json"), DateOnly.FromDateTime(DateTime.Now));
        _tracker = new GameTracker(_store, NativeMethods.ReadClock, GameDetector.Detect);
        _tray = new TrayManager(OpenSettings, TogglePause, RequestExit);
        MainForm = _dispatcher;
        _ = _dispatcher.Handle;
        _dispatcher.ShuttingDown += () => SampleAndSave(enforce: false);
        _dispatcher.SessionEnded += ExitThread;
        _poll.Tick += (_, _) => SampleAndSave(forceSave: false);
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
            _settingsForm = new SettingsForm(_settings, _directory, SaveSettings, Preview, ChangeGameTime, RemoveGame);
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
            MessageBox.Show(_settingsForm, error.Message, UiText.Get("Не удалось сохранить настройки"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        SampleAndSave(enforce: false);
        bool autoStartChanged = settings.AutoStart != _settings.AutoStart;
        _settings = settings;
        UiText.Language = settings.Language;
        _poll.Interval = settings.PollIntervalSeconds * 1000;
        _tracker.Rebase(_settings, Unavailable);
        EnforceRules();
        UpdateViews();
        if (!autoStartChanged && !settings.AutoStart)
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
            MessageBox.Show(_settingsForm,
                UiText.Get("Настройки сохранены, но автозапуск изменить не удалось.\n") + error.Message,
                UiText.Get("Автозапуск"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void SampleAndSave(bool enforce = true, bool forceSave = true)
    {
        try
        {
            _tracker.Poll(_settings, Unavailable);
            _trackingError = null;
        }
        catch (Win32Exception error)
        {
            _trackingError = UiText.Get("Ошибка определения игры: ") + error.Message;
            _tracker.Rebase(_settings, paused: true);
        }
        SaveTime(forceSave);
        if (enforce)
            EnforceRules();
        UpdateViews();
    }

    private void EnforceRules()
    {
        if (_paused || _suspended)
            return;
        if (_noticeDate != _store.Today.Date)
        {
            _noticeDate = _store.Today.Date;
            _notifiedLimits.Clear();
        }
        var reached = ProcessEnforcer.GetReachedLimits(_settings, _store.Today, DateTime.Now, terminate: false);
        var newWarnings = reached.Except(_notifiedLimits, StringComparer.OrdinalIgnoreCase).ToList();
        _notifiedLimits = reached;
        if (newWarnings.Count > 0)
            _tray.Notify(UiText.Get("Достигнут дневной лимит: ") + string.Join(", ", newWarnings));
        var targets = ProcessEnforcer.GetTargets(_settings, _store.Today, DateTime.Now);
        var result = _enforcer.Enforce(targets);
        if (result.Stopped.Count > 0)
        {
            _tracker.ExcludeTerminated(result.Stopped);
            _tray.Notify(UiText.Get("Принудительно завершены: ") + string.Join(", ", result.Stopped));
        }
        string? error = result.Errors.Count == 0 ? null
            : UiText.Get("Не удалось завершить: ") + string.Join("; ", result.Errors);
        if (error is not null && error != _terminationError)
            _tray.Notify(error);
        _terminationError = error;
    }

    private bool RemoveGame(DateOnly date, string game)
    {
        SampleAndSave(enforce: false);
        try
        {
            if (_store.Today.Date != date)
                throw new InvalidDataException(UiText.Get("Дата изменилась. Выберите приложение заново."));
            var settings = _settings.Copy();
            settings.Games.RemoveAll(name => name.Equals(game, StringComparison.OrdinalIgnoreCase));
            settings.AppLimits.Remove(game);
            if (!SaveSettings(settings))
                return false;
            if (_store.Today.GameSeconds.ContainsKey(game))
                _store.SetGameTime(date, game, null);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(_settingsForm, error.Message, UiText.Get("Удаление не выполнено"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            UpdateViews();
        }
    }

    private void ChangeGameTime(DateOnly date, string game, double? seconds)
    {
        SampleAndSave(enforce: false);
        try
        {
            _store.SetGameTime(date, game, seconds);
            _storageError = null;
            EnforceRules();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(_settingsForm, error.Message, UiText.Get("Изменение времени не сохранено"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateViews();
    }

    private bool SaveTime(bool force = true)
    {
        if (!force && _storageError is null && Environment.TickCount64 - _lastSaveTick < 60_000)
            return true;
        try
        {
            _store.Save();
            _storageError = null;
            _lastSaveTick = Environment.TickCount64;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            string message = UiText.Get("Статистика не сохранена: ") + error.Message;
            if (_storageError != message)
                _tray.Notify(message);
            _storageError = message;
            return false;
        }
    }

    private void TogglePause()
    {
        SampleAndSave(enforce: false);
        _paused = !_paused;
        _tracker.Rebase(_settings, Unavailable);
        _terminationError = null;
        EnforceRules();
        EndPreview();
    }

    private string Status()
    {
        if (_storageError is not null)
            return _storageError;
        if (_trackingError is not null)
            return _trackingError;
        if (_terminationError is not null)
            return _terminationError;
        if (_paused)
            return UiText.Get("Учёт и принудительное завершение на паузе");
        if (_locked || _suspended)
            return UiText.Get("Учёт приостановлен: сессия недоступна");
        if (_settings.Games.Count == 0)
            return UiText.Get("Добавьте приложения в список ниже");
        if (_tracker.Current.Warning is not null)
            return _tracker.Current.Warning;
        if (_settings.Monitor.Length > 0 && !Screen.AllScreens.Any(s => s.DeviceName == _settings.Monitor))
            return UiText.Get("Выбранный монитор отключён; таймер перенесён на основной");
        if (_tracker.Current.Games.Count == 0)
            return UiText.Format("Ожидание приложения · опрос раз в {0} сек.", _settings.PollIntervalSeconds);
        return UiText.Get("Учитываются: ") + string.Join(", ", _tracker.Current.Games);
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
                EnforceRules();
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
            EnforceRules();
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
            EnforceRules();
            UpdateViews();
        });
    }

    private void OnDisplaysChanged(object? sender, EventArgs args) => Post(UpdateViews);

    private void RequestExit()
    {
        SampleAndSave(enforce: false);
        if (_storageError is not null)
        {
            var result = MessageBox.Show(
                _storageError + UiText.Get("\nВыйти с потерей несохранённого времени?"), "OneMoreTimer",
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
