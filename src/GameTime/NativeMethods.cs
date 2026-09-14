using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GameTime;

internal static class NativeMethods
{
    internal const int OverlayStyles = 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080;

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong ticks);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int cx, int cy, uint flags);

    internal static ClockSample ReadClock()
    {
        if (!QueryUnbiasedInterruptTime(out ulong ticks))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new ClockSample(DateTimeOffset.Now, ticks);
    }

    internal static string? ForegroundName(nint window)
    {
        if (window == 0 || GetWindowThreadProcessId(window, out uint pid) == 0)
            return null;
        // Query image name only. No VM_READ, module enumeration, input hooks or process injection.
        using var process = OpenProcess(0x1000, false, pid);
        if (process.IsInvalid)
            return null;
        var name = new StringBuilder(32768);
        uint size = (uint)name.Capacity;
        return QueryFullProcessImageName(process, 0, name, ref size) ? Path.GetFileName(name.ToString()) : null;
    }

    internal static void KeepOnTop(nint window)
    {
        SetWindowPos(window, new nint(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002);
    }

    internal static bool TerminateMatchingProcess(int processId, string expectedName)
    {
        if (processId <= 4 || processId == Environment.ProcessId)
            return false;
        if (!ProcessIdToSessionId((uint)Environment.ProcessId, out uint ownSession)
            || !ProcessIdToSessionId((uint)processId, out uint session) || session == 0 || session != ownSession)
            return false;
        // Hold one handle for identity verification and termination to avoid acting on a reused PID.
        using var process = OpenProcess(0x1000 | 0x0001, false, (uint)processId);
        if (process.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 87)
                return false;
            throw new Win32Exception(error);
        }
        var path = new StringBuilder(32768);
        uint size = (uint)path.Capacity;
        if (!QueryFullProcessImageName(process, 0, path, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!Path.GetFileName(path.ToString()).Equals(expectedName, StringComparison.OrdinalIgnoreCase)
            || !ProcessIdToSessionId((uint)processId, out session) || session != ownSession)
            return false;
        if (!TerminateProcess(process, 1))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return true;
    }
}

internal static class GameDetector
{
    internal static GameSnapshot Detect(AppSettings settings)
    {
        if (settings.Games.Count == 0)
            return GameSnapshot.Empty;
        var wanted = settings.Games.Where(settings.IsTracked).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
            return GameSnapshot.Empty;
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        nint foreground = NativeMethods.GetForegroundWindow();
        string? activeName = NativeMethods.ForegroundName(foreground);
        bool activeGame = activeName is not null && wanted.Contains(activeName);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string name = process.ProcessName + ".exe";
                    if (wanted.Contains(name))
                        found.Add(wanted.First(game => game.Equals(name, StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException)
                {
                    // Processes may exit during enumeration or deny access; never request extra privileges.
                }
            }
        }
        if (activeGame)
            found.Add(wanted.First(name => name.Equals(activeName, StringComparison.OrdinalIgnoreCase)));
        bool hasRunningGame = found.Count > 0;
        var runningGames = new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
        string? warning = null;
        if (wanted.Any(name => settings.TrackingFor(name) == TrackingMode.Foreground)
            && foreground != 0 && activeName is null)
            warning = UiText.Get("Активный процесс недоступен для определения.");
        found = SelectCounted(settings, found, activeName);
        return new GameSnapshot(found, activeGame ? foreground : 0, warning)
        {
            HasRunningGame = hasRunningGame, RunningGames = runningGames
        };
    }

    internal static HashSet<string> SelectCounted(AppSettings settings, IEnumerable<string> running, string? activeName)
        => running.Where(name => settings.IsTracked(name) && (settings.TrackingFor(name) == TrackingMode.Running
            || name.Equals(activeName, StringComparison.OrdinalIgnoreCase)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
