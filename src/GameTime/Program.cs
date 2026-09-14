using System.Security.Principal;
using System.Text.Json;

namespace GameTime;

internal static class Program
{
    // Retain the legacy mutex and data directory for compatibility with existing installations.
    internal static string MutexName => @"Local\GameTime-" + WindowsIdentity.GetCurrent().User!.Value;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool tray = args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (!created)
        {
            if (!tray)
            {
                MessageBox.Show(
                    UiText.Get("OneMoreTimer уже работает. Откройте настройки через значок в системном трее."),
                    "OneMoreTimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameTime");
        try
        {
            using var context = new GameTimeContext(directory, tray);
            Application.Run(context);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException
            or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(
                UiText.Get("Не удалось запустить OneMoreTimer.\n") + error.Message
                    + UiText.Get("\n\nДанные: ") + directory,
                "OneMoreTimer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
