namespace GameTime;

internal readonly record struct ClockSample(DateTimeOffset WallTime, ulong AwakeTicks)
{
    public DateOnly Date => DateOnly.FromDateTime(WallTime.DateTime);
}

internal sealed record GameSnapshot(HashSet<string> Games, nint GameWindow, string? Warning = null)
{
    public static GameSnapshot Empty => new(new(StringComparer.OrdinalIgnoreCase), 0);
    public bool HasRunningGame { get; init; }
}

internal sealed class GameTracker
{
    private readonly TimeStore _store;
    private readonly Func<ClockSample> _clock;
    private readonly Func<AppSettings, GameSnapshot> _detect;
    private ClockSample? _last;

    public GameSnapshot Current { get; private set; } = GameSnapshot.Empty;

    public GameTracker(TimeStore store, Func<ClockSample> clock, Func<AppSettings, GameSnapshot> detect)
    {
        _store = store;
        _clock = clock;
        _detect = detect;
    }

    public void Poll(AppSettings settings, bool paused)
    {
        ClockSample now = _clock();
        _store.EnsureDate(now.Date);
        if (_last is { } previous && now.AwakeTicks >= previous.AwakeTicks)
        {
            double elapsed = (now.AwakeTicks - previous.AwakeTicks) / 10_000_000.0;
            double wallElapsed = (now.WallTime - previous.WallTime).TotalSeconds;
            bool continuous = elapsed <= 90 && Math.Abs(elapsed - wallElapsed) <= 5;
            if (continuous)
            {
                // Attribute the observed interval to the previous sample, never multiply the daily total.
                if (previous.Date != now.Date)
                    elapsed = Math.Min(elapsed, now.WallTime.TimeOfDay.TotalSeconds);
                _store.Add(Current.Games, elapsed);
            }
        }
        _last = now;
        Current = paused ? GameSnapshot.Empty : _detect(settings);
    }

    public void Rebase(AppSettings settings, bool paused)
    {
        _last = null;
        Current = GameSnapshot.Empty;
        Poll(settings, paused);
    }
}
