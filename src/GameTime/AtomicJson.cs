using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameTime;

internal static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Read<T>(string path, Func<T> create, Action<T> validate, out string? warning)
    {
        warning = null;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
            return create();
        try
        {
            return ReadOne(path, validate);
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException)
        {
            if (!File.Exists(path + ".bak"))
                throw new InvalidDataException(UiText.Format("Не удалось прочитать {0}. Данные не перезаписаны.", path),
                    error);
            T recovered = ReadOne(path + ".bak", validate);
            warning = UiText.Format("Восстановлена резервная копия {0}. Последняя запись могла потеряться.",
                Path.GetFileName(path));
            // Keep the damaged primary for inspection; do not turn it into the next backup.
            if (File.Exists(path))
            {
                string damaged = path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Move(path, damaged);
            }
            return recovered;
        }
    }

    private static T ReadOne<T>(string path, Action<T> validate)
    {
        using var stream = File.OpenRead(path);
        T value = JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidDataException(UiText.Get("JSON не содержит данных."));
        validate(value);
        return value;
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, Options);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(path))
            File.Replace(temporary, path, path + ".bak");
        else
            File.Move(temporary, path);
    }
}
