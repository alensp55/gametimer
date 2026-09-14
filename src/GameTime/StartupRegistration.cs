using Microsoft.Win32;

namespace GameTime;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "OneMoreTimer";
    private const string LegacyName = "GameTime";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue(Name) is string || key?.GetValue(LegacyName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, writable: true);
        if (enabled)
        {
            string exe = Environment.ProcessPath
                ?? throw new IOException(UiText.Get("Не удалось определить путь приложения."));
            if (Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException(UiText.Get("Для автозапуска запустите собранный OneMoreTimer.exe."));
            key.SetValue(Name, $"\"{exe}\" --tray", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(Name, throwOnMissingValue: false);
        }
        key.DeleteValue(LegacyName, throwOnMissingValue: false);
    }
}
