namespace GameTime;

internal sealed class DailyStats
{
    public DateOnly Date { get; set; }
    public double TotalSeconds { get; set; }
    public Dictionary<string, double> GameSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        if (Date == default || !double.IsFinite(TotalSeconds) || TotalSeconds < 0 || GameSeconds is null)
            throw new InvalidDataException(UiText.Get("Некорректная дневная статистика."));
        var normalized = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (game, seconds) in GameSeconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0)
                throw new InvalidDataException(UiText.Get("Некорректное время игры."));
            if (!normalized.TryAdd(AppSettings.NormalizeGame(game), seconds))
                throw new InvalidDataException(UiText.Get("Повторяющаяся игра в статистике."));
        }
        GameSeconds = normalized;
    }
}

internal sealed class TimeStore
{
    private readonly string _path;
    private bool _dirty;

    public DailyStats Today { get; private set; }
    public string? RecoveryWarning { get; }

    public TimeStore(string path, DateOnly date)
    {
        _path = path;
        Today = AtomicJson.Read(path, () => new DailyStats { Date = date }, value => value.Validate(), out var warning);
        RecoveryWarning = warning;
        _dirty = warning is not null;
        EnsureDate(date);
    }

    public void EnsureDate(DateOnly date)
    {
        if (Today.Date == date)
            return;
        Today = new DailyStats { Date = date };
        _dirty = true;
    }

    public void Add(IReadOnlyCollection<string> games, double seconds)
    {
        if (games.Count == 0 || seconds <= 0)
            return;
        Today.TotalSeconds += seconds;
        foreach (string game in games.Distinct(StringComparer.OrdinalIgnoreCase))
            Today.GameSeconds[game] = Today.GameSeconds.GetValueOrDefault(game) + seconds;
        _dirty = true;
    }

    public void Save()
    {
        if (!_dirty)
            return;
        AtomicJson.Write(_path, Today);
        _dirty = false;
    }

    public void SetGameTime(DateOnly date, string game, double? seconds)
    {
        DailyStats corrected = CopyToday(date);
        game = AppSettings.NormalizeGame(game);
        if (!corrected.GameSeconds.TryGetValue(game, out double previousSeconds) && seconds is null)
            throw new InvalidDataException(UiText.Get("Строка уже отсутствует. Выберите игру заново."));
        if (seconds is { } value)
        {
            if (!double.IsFinite(value) || value < 0)
                throw new InvalidDataException(UiText.Get("Некорректное время игры."));
            corrected.GameSeconds[game] = value;
        }
        else
            corrected.GameSeconds.Remove(game);
        double adjustedTotal = corrected.TotalSeconds + (seconds ?? 0) - previousSeconds;
        // Exact overlaps are not stored; keep the adjusted total within the remaining games' bounds.
        double minimum = corrected.GameSeconds.Values.DefaultIfEmpty(0).Max();
        double maximum = corrected.GameSeconds.Values.Sum();
        corrected.TotalSeconds = Math.Clamp(adjustedTotal, minimum, maximum);
        SaveCorrection(corrected);
    }

    private DailyStats CopyToday(DateOnly date)
    {
        if (Today.Date != date)
            throw new InvalidDataException(UiText.Get("Дата изменилась. Откройте редактирование времени заново."));
        return new DailyStats
        {
            Date = Today.Date, TotalSeconds = Today.TotalSeconds,
            GameSeconds = new Dictionary<string, double>(Today.GameSeconds, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void SaveCorrection(DailyStats corrected)
    {
        corrected.Validate();
        AtomicJson.Write(_path, corrected);
        Today = corrected;
        _dirty = false;
    }
}
