using System.Text.Json.Serialization;

namespace GameTime;

internal enum TrackingMode { Running, Foreground }
internal enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

internal sealed class AppSettings
{
    public List<string> Games { get; set; } = [];
    public TrackingMode Mode { get; set; } = TrackingMode.Running;
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
        if (Games is null || Monitor is null)
            throw new InvalidDataException("Некорректный список игр или монитор.");
        Games = Games.Select(NormalizeGame).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Corner))
            throw new InvalidDataException("Неизвестный режим учёта или положение окна.");
        if (LimitMinutes is < 0 or > 1440 || OpacityPercent is < 30 or > 100)
            throw new InvalidDataException("Ориентир: 0 (выключен) или 1–1440 минут. Непрозрачность: 30–100%.");
        if (OffsetX is < 0 or > 10000 || OffsetY is < 0 or > 10000)
            throw new InvalidDataException("Отступы должны быть от 0 до 10000.");
    }

    public static string NormalizeGame(string? value)
    {
        string name = value?.Trim() ?? "";
        if (name.Length <= 4 || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Укажите имя файла с расширением .exe, например Darktide.exe.");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Length > 255)
            throw new InvalidDataException("Нужно имя .exe без пути и специальных символов.");
        return name;
    }
}
