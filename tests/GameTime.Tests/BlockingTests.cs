using System.Diagnostics;
using System.Reflection;
using GameTime;

internal static partial class TestRunner
{
    private static void BlockingTests()
    {
        Check("legacy settings keep enforcement off and polling defaults to 60 seconds", () =>
        {
            string path = Path.Combine(_directory, "legacy-blocking.json");
            File.WriteAllText(path, "{\"Games\":[\"one.exe\"],\"LimitMinutes\":60}");
            var settings = AtomicJson.Read(path, () => new AppSettings(), value => value.Validate(), out _);
            Equal(60, settings.PollIntervalSeconds);
            Equal(1, settings.AppLimits.Count);
            True(!settings.AppLimits["one.exe"].HasLimit);
            True(!settings.Schedule.Enabled);
            Throws<InvalidDataException>(() => new AppSettings { PollIntervalSeconds = 0 }.Validate());
            Throws<InvalidDataException>(() => new AppSettings { PollIntervalSeconds = 3601 }.Validate());
            foreach (int interval in new[] { 1, 120, 3600 })
            {
                var rig = new Rig("interval-" + interval) { Games = ["one.exe"] };
                rig.Settings.PollIntervalSeconds = interval;
                rig.Settings.Validate();
                rig.Poll();
                rig.Advance(interval);
                rig.Poll();
                Near(interval, rig.Store.Today.TotalSeconds);
            }
        });
        Check("only enabled per-app limits use that app's time and reset next day", () =>
        {
            var now = new DateTime(2026, 9, 14, 12, 0, 0);
            var settings = new AppSettings
            {
                Games = ["one.exe", "two.exe"], LimitMinutes = 1,
                AppLimits = new()
                {
                    ["ONE.EXE"] = new() { Terminate = true, Minutes = 10 },
                    ["two.exe"] = new() { Minutes = 1 }
                }
            };
            settings.Validate();
            var today = new DailyStats
            {
                Date = DateOnly.FromDateTime(now), TotalSeconds = 10000,
                GameSeconds = new() { ["one.exe"] = 599.9, ["two.exe"] = 9000 }
            };
            today.Validate();
            Equal(0, ProcessEnforcer.GetTargets(settings, today, now).Count);
            today.GameSeconds["one.exe"] = 600;
            True(ProcessEnforcer.GetTargets(settings, today, now).SetEquals(["one.exe"]));
            Equal(0, ProcessEnforcer.GetTargets(settings, today, now.AddDays(1)).Count);
            settings.Games.Remove("one.exe");
            Equal(0, ProcessEnforcer.GetTargets(settings, today, now).Count);
            Throws<InvalidDataException>(settings.Validate);
            Throws<InvalidDataException>(() => ProcessEnforcer.ValidateTarget("explorer.exe"));
            Throws<InvalidDataException>(() => ProcessEnforcer.ValidateTarget("GameTime.exe"));
            Throws<InvalidDataException>(() => ProcessEnforcer.ValidateTarget("OneMoreTimer.exe"));
        });
        Check("weekly schedule includes start, excludes end and carries over midnight", () =>
        {
            var monday = new DateTime(2026, 9, 14);
            var period = new SchedulePeriod { Days = 1 << (int)DayOfWeek.Monday, StartMinute = 1320, EndMinute = 420 };
            period.Validate();
            True(!period.IsActive(monday.AddHours(21).AddMinutes(59)));
            True(period.IsActive(monday.AddHours(22)));
            True(period.IsActive(monday.AddDays(1).AddHours(6).AddMinutes(59)));
            True(!period.IsActive(monday.AddDays(1).AddHours(7)));
            True(!period.IsActive(monday.AddDays(1).AddHours(22)));
            var schedule = new BlockingSchedule { Enabled = true, Applications = ["one.exe"], Periods = [period] };
            var settings = new AppSettings { Schedule = schedule };
            settings.Validate();
            var today = new DailyStats { Date = DateOnly.FromDateTime(monday) };
            True(ProcessEnforcer.GetTargets(settings, today, monday.AddHours(23)).SetEquals(["one.exe"]));
            settings.AppLimits["one.exe"] = settings.AppLimits["one.exe"] with { ScheduleMode = AppScheduleMode.None };
            Equal(0, ProcessEnforcer.GetTargets(settings, today, monday.AddHours(23)).Count);
            var allDay = period with { StartMinute = 0, EndMinute = 0 };
            True(allDay.IsActive(monday));
            True(!allDay.IsActive(monday.AddDays(1)));
            var daytime = period with { StartMinute = 600, EndMinute = 660 };
            True(daytime.IsActive(monday.AddHours(10)));
            True(!daytime.IsActive(monday.AddHours(11)));
            Throws<InvalidDataException>(() => new SchedulePeriod { Days = 0 }.Validate());
        });
        Check("new zero-time rows can be edited and stopped apps do not accrue another interval", () =>
        {
            var rig = new Rig("stopped") { Games = ["one.exe", "two.exe"] };
            rig.Poll();
            rig.Advance(60);
            rig.Poll();
            rig.Tracker.ExcludeTerminated(new HashSet<string>(["one.exe"], StringComparer.OrdinalIgnoreCase));
            rig.Games = ["two.exe"];
            rig.Advance(60);
            rig.Poll();
            Near(60, rig.Store.Today.GameSeconds["one.exe"]);
            Near(120, rig.Store.Today.GameSeconds["two.exe"]);
            rig.Store.SetGameTime(rig.Store.Today.Date, "new.exe", 180);
            Near(180, rig.Store.Today.GameSeconds["new.exe"]);
            Near(300, rig.Store.Today.TotalSeconds);
        });
        Check("forced termination verifies executable identity and leaves other processes running", () =>
        {
            using var target = new TerminationProbe();
            using var other = new TerminationProbe();
            True(!NativeMethods.TerminateMatchingProcess(target.Process.Id, other.Name));
            True(!target.Process.HasExited);
            True(!NativeMethods.TerminateMatchingProcess(Environment.ProcessId, target.Name));
            var result = new ProcessEnforcer().Enforce([target.Name]);
            True(result.Stopped.SetEquals([target.Name]));
            Equal(0, result.Errors.Count);
            True(target.Process.WaitForExit(5000));
            True(!other.Process.HasExited);
        });
        Check("context enforces schedules and pause suspends and resumes termination", () =>
        {
            using var probe = new TerminationProbe();
            string directory = Path.Combine(_directory, "schedule-context");
            var settings = new AppSettings
            {
                Schedule = new()
                {
                    Applications = [probe.Name], Periods = [new() { StartMinute = 0, EndMinute = 0 }]
                }
            };
            AtomicJson.Write(Path.Combine(directory, "settings.json"), settings);
            using var context = new GameTimeContext(directory, startInTray: true);
            InvokeContext(context, "TogglePause");
            settings.Schedule.Enabled = true;
            True((bool)InvokeContext(context, "SaveSettings", settings)!);
            InvokeContext(context, "SampleAndSave", true, true);
            True(!probe.Process.HasExited);
            InvokeContext(context, "TogglePause");
            True(probe.Process.WaitForExit(5000));
        });
        Check("removing a running app persists removal and does not recreate its daily row", () =>
        {
            using var probe = new TerminationProbe();
            string directory = Path.Combine(_directory, "remove-context");
            string settingsPath = Path.Combine(directory, "settings.json");
            string timePath = Path.Combine(directory, "today.json");
            AtomicJson.Write(settingsPath, new AppSettings { Games = [probe.Name] });
            var store = new TimeStore(timePath, DateOnly.FromDateTime(DateTime.Now));
            store.Add([probe.Name], 120);
            store.Save();
            using var context = new GameTimeContext(directory, startInTray: true);
            True((bool)InvokeContext(context, "RemoveGame", store.Today.Date, probe.Name)!);
            InvokeContext(context, "SampleAndSave", true, true);
            var saved = AtomicJson.Read(settingsPath, () => new AppSettings(), value => value.Validate(), out _);
            True(!saved.Games.Contains(probe.Name));
            var restored = new TimeStore(timePath, store.Today.Date);
            Equal(0, restored.Today.GameSeconds.Count);
            Near(0, restored.Today.TotalSeconds);
            True(!probe.Process.HasExited);
        });
    }

    private static object? InvokeContext(GameTimeContext context, string method, params object[] args)
        => typeof(GameTimeContext).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(context, args);

    private sealed class TerminationProbe : IDisposable
    {
        private readonly string _path;
        public Process Process { get; }
        public string Name => Path.GetFileName(_path);

        public TerminationProbe()
        {
            _path = Path.Combine(AppContext.BaseDirectory, "GameTime.Probe-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Environment.ProcessPath!, _path);
            Process = Process.Start(new ProcessStartInfo(_path, "--termination-probe")
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true
            })!;
            True(Process.StandardOutput.ReadLineAsync().Wait(5000));
            True(!Process.HasExited);
        }

        public void Dispose()
        {
            if (!Process.HasExited)
                Process.Kill();
            Process.WaitForExit(5000);
            Process.Dispose();
            File.Delete(_path);
        }
    }
}
