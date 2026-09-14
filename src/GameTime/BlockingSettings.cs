using System.Text.Json.Serialization;

namespace GameTime;

internal enum AppScheduleMode { None, Shared, Custom }

internal sealed record AppTimeLimit
{
    public bool Enabled { get; set; } = true;
    public bool Track { get; set; } = true;
    public TrackingMode? Mode { get; set; }
    public bool? UseLimit { get; set; }
    public bool Terminate { get; set; }
    public int Minutes { get; set; } = 120;
    public AppScheduleMode ScheduleMode { get; set; }
    public List<SchedulePeriod> CustomPeriods { get; set; } = [];

    [JsonIgnore]
    public bool HasLimit => UseLimit ?? Terminate;

    public AppTimeLimit Copy()
        => this with { CustomPeriods = CustomPeriods.Select(period => period with { }).ToList() };
}

internal sealed record SchedulePeriod
{
    public int Days { get; set; } = 127;
    public int StartMinute { get; set; } = 22 * 60;
    public int EndMinute { get; set; } = 7 * 60;

    public void Validate()
    {
        if (Days is < 1 or > 127 || StartMinute is < 0 or > 1439 || EndMinute is < 0 or > 1439)
            throw new InvalidDataException(UiText.Get("Укажите дни недели и время расписания."));
    }

    public bool IsActive(DateTime now)
    {
        double minute = now.TimeOfDay.TotalMinutes;
        if (StartMinute == EndMinute)
            return Includes(now.DayOfWeek);
        if (StartMinute < EndMinute)
            return Includes(now.DayOfWeek) && minute >= StartMinute && minute < EndMinute;
        return (Includes(now.DayOfWeek) && minute >= StartMinute)
            || (Includes(now.AddDays(-1).DayOfWeek) && minute < EndMinute);
    }

    public bool Includes(DayOfWeek day) => (Days & (1 << (int)day)) != 0;

    [JsonIgnore]
    public string DayText => string.Join(", ", Enumerable.Range(0, 7).Select(i => (i + 1) % 7)
        .Where(day => Includes((DayOfWeek)day)).Select(day => DayNames[day]));

    public static string[] DayNames => UiText.Language == AppLanguage.Russian
        ? ["Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"] : ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static string FormatTime(int minute) => $"{minute / 60:00}:{minute % 60:00}";
}

internal sealed class BlockingSchedule
{
    public bool Enabled { get; set; }
    public List<string> Applications { get; set; } = [];
    public List<SchedulePeriod> Periods { get; set; } = [];

}
