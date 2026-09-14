using System.Text.Json.Serialization;

namespace GameTime;

internal enum TrackingMode { Running, Foreground }
internal enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

internal sealed class AppSettings
{
    public int FormatVersion { get; set; }
    public List<string> Games { get; set; } = [];
    public TrackingMode Mode { get; set; } = TrackingMode.Running;
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public int PollIntervalSeconds { get; set; } = 60;
    public Dictionary<string, AppTimeLimit> AppLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public BlockingSchedule Schedule { get; set; } = new();
    public int LimitMinutes { get; set; }
    public bool OverlayEnabled { get; set; } = true;
    public bool OverlayOnlyWithGame { get; set; } = true;
    public string Monitor { get; set; } = "";
    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;
    public int OffsetX { get; set; } = 24;
    public int OffsetY { get; set; } = 24;
    public int OpacityPercent { get; set; } = 85;
    public bool PulseAt100 { get; set; } = true;

    [JsonIgnore]
    public bool AutoStart { get; set; }

    public void Validate()
    {
        if (Games is null || Monitor is null || AppLimits is null || Schedule is null)
            throw new InvalidDataException(UiText.Get("Некорректный список игр или монитор."));
        Games = Games.Select(NormalizeGame).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var limits = new Dictionary<string, AppTimeLimit>(StringComparer.OrdinalIgnoreCase);
        foreach (var (game, rule) in AppLimits)
        {
            string name = NormalizeGame(game);
            if (rule is null || rule.Minutes is < 1 or > 1440 || !limits.TryAdd(name, rule))
                throw new InvalidDataException(UiText.Get("Лимит приложения: от 1 до 1440 минут; без повторов имён."));
            if (rule.CustomPeriods is null || !Enum.IsDefined(rule.ScheduleMode)
                || (rule.Mode is { } mode && !Enum.IsDefined(mode)))
                throw new InvalidDataException(UiText.Get("Некорректное правило приложения."));
            foreach (var period in rule.CustomPeriods)
            {
                if (period is null)
                    throw new InvalidDataException(UiText.Get("Некорректный интервал расписания."));
                period.Validate();
            }
            if (rule.HasLimit)
            {
                if (rule.Terminate)
                    ProcessEnforcer.ValidateTarget(name);
                if (!rule.Track || !Games.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        UiText.Get("Для завершения по лимиту включите учёт времени приложения."));
            }
            if (rule.ScheduleMode != AppScheduleMode.None)
                ProcessEnforcer.ValidateTarget(name);
        }
        AppLimits = limits;
        if (Schedule.Applications is null || Schedule.Periods is null)
            throw new InvalidDataException(UiText.Get("Некорректное расписание."));
        Schedule.Applications = Schedule.Applications.Select(NormalizeGame)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string name in Schedule.Applications)
            ProcessEnforcer.ValidateTarget(name);
        foreach (var period in Schedule.Periods)
        {
            if (period is null)
                throw new InvalidDataException(UiText.Get("Некорректный интервал расписания."));
            period.Validate();
        }
        if (FormatVersion is < 0 or > 1)
            throw new InvalidDataException(UiText.Get("Неизвестная версия настроек."));
        if (FormatVersion == 0)
        {
            foreach (string name in Games)
            {
                var rule = AppLimits.GetValueOrDefault(name) ?? new AppTimeLimit();
                AppLimits[name] = rule with { Mode = rule.Mode ?? Mode, UseLimit = rule.HasLimit };
            }
            foreach (string name in Schedule.Applications)
            {
                if (!Games.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    Games.Add(name);
                    AppLimits[name] = new AppTimeLimit { Track = false, Mode = Mode, UseLimit = false };
                }
                if (Schedule.Enabled)
                    AppLimits[name].ScheduleMode = AppScheduleMode.Shared;
            }
            foreach (string name in AppLimits.Keys.Except(Games, StringComparer.OrdinalIgnoreCase).ToList())
            {
                Games.Add(name);
                AppLimits[name] = AppLimits[name] with { Track = false, UseLimit = false };
            }
            Schedule.Applications.Clear();
            FormatVersion = 1;
        }
        foreach (string name in Games)
            AppLimits.TryAdd(name, new AppTimeLimit { Mode = Mode, UseLimit = false });
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Corner) || !Enum.IsDefined(Language))
            throw new InvalidDataException(UiText.Get("Неизвестный режим учёта или положение окна."));
        if (PollIntervalSeconds is < 1 or > 3600)
            throw new InvalidDataException(UiText.Get("Интервал опроса: от 1 до 3600 секунд."));
        if (LimitMinutes is < 0 or > 1440 || OpacityPercent is < 30 or > 100)
            throw new InvalidDataException(
                UiText.Get("Ориентир: 0 (выключен) или 1–1440 минут. Непрозрачность: 30–100%."));
        if (OffsetX is < 0 or > 10000 || OffsetY is < 0 or > 10000)
            throw new InvalidDataException(UiText.Get("Отступы должны быть от 0 до 10000."));
    }

    public AppSettings Copy()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.Games = [.. Games];
        copy.AppLimits = AppLimits.ToDictionary(pair => pair.Key, pair => pair.Value.Copy(),
            StringComparer.OrdinalIgnoreCase);
        copy.Schedule = new BlockingSchedule
        {
            Enabled = Schedule.Enabled, Applications = [.. Schedule.Applications],
            Periods = Schedule.Periods.Select(period => period with { }).ToList()
        };
        return copy;
    }

    public bool IsTracked(string name) => Games.Contains(name, StringComparer.OrdinalIgnoreCase)
        && (!AppLimits.TryGetValue(name, out var rule) || (rule.Enabled && rule.Track));

    public TrackingMode TrackingFor(string name) => AppLimits.GetValueOrDefault(name)?.Mode ?? Mode;

    public IReadOnlyList<SchedulePeriod> PeriodsFor(AppTimeLimit rule) => rule.ScheduleMode switch
    {
        AppScheduleMode.Shared => Schedule.Periods,
        AppScheduleMode.Custom => rule.CustomPeriods,
        _ => []
    };

    public static string NormalizeGame(string? value)
    {
        string name = value?.Trim() ?? "";
        if (name.Length <= 4 || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(UiText.Get("Укажите имя файла с расширением .exe, например Darktide.exe."));
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Length > 255)
            throw new InvalidDataException(UiText.Get("Нужно имя .exe без пути и специальных символов."));
        return name;
    }
}
