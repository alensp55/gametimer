namespace GameTime;

internal static class TimerDisplay
{
    public static string Duration(double seconds)
    {
        long minutes = (long)(seconds / 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    public static string Text(double seconds, int limitMinutes)
    {
        if (limitMinutes == 0)
            return Duration(seconds);
        string text = $"{Duration(seconds)} / {Duration(limitMinutes * 60)}";
        int over = (int)((seconds - limitMinutes * 60) / 60);
        return over > 0 ? $"{text}  +{over} {UiText.Get("мин")}" : text;
    }

    public static bool LimitReached(double seconds, AppSettings settings)
        => settings.LimitMinutes > 0 && seconds >= settings.LimitMinutes * 60;
}
