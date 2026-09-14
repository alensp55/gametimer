using System.ComponentModel;
using System.Diagnostics;

namespace GameTime;

internal sealed record TerminationResult(HashSet<string> Stopped, List<string> Errors);

internal sealed class ProcessEnforcer
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameTime.exe", "OneMoreTimer.exe", "explorer.exe", "dwm.exe", "winlogon.exe", "csrss.exe", "lsass.exe",
        "services.exe", "smss.exe", "wininit.exe", "svchost.exe", "sihost.exe", Path.GetFileName(Environment.ProcessPath!)
    };

    public static void ValidateTarget(string name)
    {
        AppSettings.NormalizeGame(name);
        if (ProtectedNames.Contains(name))
            throw new InvalidDataException(UiText.Format("Нельзя назначить принудительное завершение для {0}.", name));
    }

    public static HashSet<string> GetTargets(AppSettings settings, DailyStats today, DateTime now)
    {
        var targets = GetReachedLimits(settings, today, now, terminate: true);
        foreach (string name in settings.Games)
            if (settings.AppLimits.TryGetValue(name, out var rule) && rule.Enabled
                && settings.PeriodsFor(rule).Any(period => period.IsActive(now)))
                targets.Add(name);
        targets.RemoveWhere(ProtectedNames.Contains);
        return targets;
    }

    public static HashSet<string> GetReachedLimits(AppSettings settings, DailyStats today, DateTime now, bool terminate)
    {
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (today.Date == DateOnly.FromDateTime(now))
            foreach (var (name, rule) in settings.AppLimits)
                if (rule.HasLimit && rule.Terminate == terminate && settings.IsTracked(name)
                    && today.GameSeconds.GetValueOrDefault(name) >= rule.Minutes * 60)
                    reached.Add(name);
        return reached;
    }

    public TerminationResult Enforce(IReadOnlyCollection<string> targets)
    {
        var stopped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (string name in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ProtectedNames.Contains(name))
                continue;
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name));
            }
            catch (Win32Exception error)
            {
                errors.Add($"{name}: {error.Message}");
                continue;
            }
            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (NativeMethods.TerminateMatchingProcess(process.Id, name))
                            stopped.Add(name);
                    }
                    catch (Win32Exception error)
                    {
                        errors.Add($"{name}: {error.Message}");
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited between enumeration and opening its handle.
                    }
                }
            }
        }
        return new TerminationResult(stopped, errors.Distinct().ToList());
    }
}
