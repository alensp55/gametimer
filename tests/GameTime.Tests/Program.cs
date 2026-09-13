using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameTime;

internal static class TestRunner
{
    private static int _passed;
    private static int _failed;
    private static string _directory = "";

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint window, int index);

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        _directory = Path.GetFullPath(Path.Combine("artifacts", "tests", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(_directory);
        CoreTests();
        StorageTests();
        CorrectionTests();
        UiTests();
        if (args.Contains("--live"))
            Check("real minute polling and persistence", LiveMinute);
        int exeIndex = Array.IndexOf(args, "--exe");
        if (exeIndex >= 0)
            Check("second instance exits before accessing data", () => SingleInstance(args[exeIndex + 1]));
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed. Artifacts: {_directory}");
        return _failed == 0 ? 0 : 1;
    }

    private static void CoreTests()
    {
        Check("case-insensitive exe list", () =>
        {
            var settings = new AppSettings { Games = [" Darktide.exe ", "DARKTIDE.EXE"] };
            settings.Validate();
            Equal(1, settings.Games.Count);
            Equal("Darktide.exe", settings.Games[0]);
            Throws<InvalidDataException>(() => AppSettings.NormalizeGame(@"C:\Games\game.exe"));
            Throws<InvalidDataException>(() => AppSettings.NormalizeGame("game"));
        });
        Check("settings boundaries", () =>
        {
            new AppSettings { LimitMinutes = 0 }.Validate();
            Throws<InvalidDataException>(() => new AppSettings { LimitMinutes = -1 }.Validate());
            Throws<InvalidDataException>(() => new AppSettings { OpacityPercent = 0 }.Validate());
            Throws<InvalidDataException>(() => new AppSettings { Mode = (TrackingMode)5 }.Validate());
            new AppSettings { LimitMinutes = 1440, OpacityPercent = 30 }.Validate();
        });
        Check("two games share one daily minute", () =>
        {
            var rig = new Rig("union");
            rig.Games = ["one.exe", "two.exe"];
            rig.Poll();
            rig.Advance(60);
            rig.Poll();
            Near(60, rig.Store.Today.TotalSeconds);
            Near(60, rig.Store.Today.GameSeconds["one.exe"]);
            Near(60, rig.Store.Today.GameSeconds["two.exe"]);
        });
        Check("exit detection stops subsequent intervals", () =>
        {
            var rig = new Rig("exit");
            rig.Games = ["one.exe"];
            rig.Poll();
            rig.Advance(60);
            rig.Games = [];
            rig.Poll();
            rig.Advance(60);
            rig.Poll();
            Near(60, rig.Store.Today.TotalSeconds);
        });
        Check("pause and resume preserve only observed play", () =>
        {
            var rig = new Rig("pause") { Games = ["one.exe"] };
            rig.Poll();
            rig.Advance(20);
            rig.Poll();
            rig.Tracker.Rebase(rig.Settings, true);
            rig.Advance(60);
            rig.Tracker.Poll(rig.Settings, true);
            Near(20, rig.Store.Today.TotalSeconds);
            rig.Tracker.Rebase(rig.Settings, false);
            rig.Advance(10);
            rig.Poll();
            Near(30, rig.Store.Today.TotalSeconds);
        });
        Check("midnight splits the current interval", () =>
        {
            var rig = new Rig("midnight") { Wall = Local(2026, 9, 13, 23, 59, 40), Games = ["one.exe"] };
            rig.Poll();
            rig.Advance(60);
            rig.Poll();
            Equal(new DateOnly(2026, 9, 14), rig.Store.Today.Date);
            Near(40, rig.Store.Today.TotalSeconds);
        });
        Check("sleep gap is excluded without power event", () =>
        {
            var rig = new Rig("sleep") { Games = ["one.exe"] };
            rig.Poll();
            rig.Wall = rig.Wall.AddHours(8);
            rig.Ticks += 10_000_000;
            rig.Poll();
            Near(0, rig.Store.Today.TotalSeconds);
            rig.Advance(60);
            rig.Poll();
            Near(60, rig.Store.Today.TotalSeconds);
        });
        foreach (int jump in new[] { -3600, 3600 })
        {
            Check($"clock jump {jump} does not create time", () =>
            {
                var rig = new Rig("clock" + jump) { Games = ["one.exe"] };
                rig.Poll();
                rig.Advance(60);
                rig.Wall = rig.Wall.AddSeconds(jump);
                rig.Poll();
                Near(0, rig.Store.Today.TotalSeconds);
            });
        }
        Check("long unobserved interval is excluded", () =>
        {
            var rig = new Rig("gap") { Games = ["one.exe"] };
            rig.Poll();
            rig.Advance(900);
            rig.Poll();
            Near(0, rig.Store.Today.TotalSeconds);
        });
        Check("mode change starts a new observation", () =>
        {
            var rig = new Rig("mode") { Games = ["one.exe"] };
            rig.Poll();
            rig.Advance(25);
            rig.Poll();
            rig.Games = [];
            rig.Settings.Mode = TrackingMode.Foreground;
            rig.Tracker.Rebase(rig.Settings, false);
            rig.Advance(60);
            rig.Poll();
            Near(25, rig.Store.Today.TotalSeconds);
        });
        Check("optional limit and overtime text", () =>
        {
            var settings = new AppSettings { LimitMinutes = 120 };
            True(!TimerDisplay.LimitReached(5760, settings));
            True(!TimerDisplay.LimitReached(7199, settings));
            True(TimerDisplay.LimitReached(7200, settings));
            Equal("02:17 / 02:00  +17 мин", TimerDisplay.Text(8220, 120));
            settings.LimitMinutes = 0;
            True(!TimerDisplay.LimitReached(0, settings));
            True(!TimerDisplay.LimitReached(90000, settings));
            Equal("01:37", TimerDisplay.Text(5820, 0));
            Equal("25:00", TimerDisplay.Duration(90000));
        });
    }

    private static void StorageTests()
    {
        Check("existing settings retain limit and ignore removed warning", () =>
        {
            string path = Path.Combine(_directory, "previous-settings.json");
            File.WriteAllText(path, "{\"LimitMinutes\":120,\"WarnAt80\":true}");
            var settings = AtomicJson.Read(path, () => new AppSettings(), value => value.Validate(), out var warning);
            Equal(120, settings.LimitMinutes);
            True(warning is null);
        });
        Check("restart restores saved time without counting downtime", () =>
        {
            string path = Path.Combine(_directory, "restart.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 61.5);
            store.Save();
            var restored = new TimeStore(path, store.Today.Date);
            Near(61.5, restored.Today.TotalSeconds);
            Near(61.5, restored.Today.GameSeconds["ONE.EXE"]);
            restored.Add(["one.exe"], 40);
            var afterCrash = new TimeStore(path, store.Today.Date);
            Near(61.5, afterCrash.Today.TotalSeconds);
        });
        Check("next-day startup clears old counters", () =>
        {
            string path = Path.Combine(_directory, "next-day.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 7200);
            store.Save();
            var next = new TimeStore(path, new DateOnly(2026, 9, 14));
            Near(0, next.Today.TotalSeconds);
            next.Save();
            Equal(new DateOnly(2026, 9, 14), new TimeStore(path, next.Today.Date).Today.Date);
        });
        Check("corrupt primary recovers last backup", () =>
        {
            string path = Path.Combine(_directory, "recover.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 60);
            store.Save();
            store.Add(["one.exe"], 60);
            store.Save();
            File.WriteAllText(path, "{truncated");
            var recovered = new TimeStore(path, store.Today.Date);
            Near(60, recovered.Today.TotalSeconds);
            True(recovered.RecoveryWarning is not null);
            recovered.Save();
            Near(60, new TimeStore(path, store.Today.Date).Today.TotalSeconds);
        });
        Check("invalid data without backup is never overwritten", () =>
        {
            string path = Path.Combine(_directory, "invalid.json");
            File.WriteAllText(path, "{broken");
            Throws<InvalidDataException>(() => new TimeStore(path, new DateOnly(2026, 9, 13)));
            Equal("{broken", File.ReadAllText(path));
        });
        Check("write failure preserves pending time for retry", () =>
        {
            string path = Path.Combine(_directory, "locked.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 60);
            store.Save();
            store.Add(["one.exe"], 20);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Throws<IOException>(store.Save);
            store.Save();
            Near(80, new TimeStore(path, store.Today.Date).Today.TotalSeconds);
        });
    }

    private static void CorrectionTests()
    {
        Check("editing and deleting a game recalculate and persist total", () =>
        {
            string path = Path.Combine(_directory, "recalculated-game.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 300);
            store.SetGameTime(store.Today.Date, "ONE.EXE", 600);
            Near(600, store.Today.TotalSeconds);
            Near(600, store.Today.GameSeconds["one.exe"]);
            var restored = new TimeStore(path, store.Today.Date);
            Near(600, restored.Today.TotalSeconds);
            Near(600, restored.Today.GameSeconds["one.exe"]);
            restored.Add(["two.exe"], 120);
            restored.SetGameTime(restored.Today.Date, "one.exe", 60);
            Near(180, restored.Today.TotalSeconds);
            restored.SetGameTime(restored.Today.Date, "one.exe", null);
            Near(120, restored.Today.TotalSeconds);
            restored.SetGameTime(restored.Today.Date, "two.exe", null);
            Equal(0, restored.Today.GameSeconds.Count);
            Near(0, new TimeStore(path, store.Today.Date).Today.TotalSeconds);
        });
        Check("deleting overlapping games retains the remaining game's time", () =>
        {
            string path = Path.Combine(_directory, "overlapping-correction.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe", "two.exe"], 300);
            store.SetGameTime(store.Today.Date, "one.exe", null);
            Near(300, store.Today.TotalSeconds);
            var restored = new TimeStore(path, store.Today.Date);
            Near(300, restored.Today.TotalSeconds);
            Near(300, restored.Today.GameSeconds["two.exe"]);
        });
        Check("failed corrections leave in-memory and pending counters intact", () =>
        {
            string path = Path.Combine(_directory, "correction-locked.json");
            var store = new TimeStore(path, new DateOnly(2026, 9, 13));
            store.Add(["one.exe"], 300);
            store.Save();
            store.Add(["one.exe"], 20);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Throws<IOException>(() => store.SetGameTime(store.Today.Date, "one.exe", null));
                Throws<IOException>(() => store.SetGameTime(store.Today.Date, "one.exe", 60));
                Near(320, store.Today.GameSeconds["one.exe"]);
                Near(320, store.Today.TotalSeconds);
            }
            store.Save();
            Near(320, new TimeStore(path, store.Today.Date).Today.TotalSeconds);
        });
        Check("invalid or stale corrections do not change current data", () =>
        {
            var store = new TimeStore(Path.Combine(_directory, "stale-correction.json"), new DateOnly(2026, 9, 13));
            store.Add(["one.exe", "two.exe"], 300);
            Throws<InvalidDataException>(() => store.SetGameTime(store.Today.Date, "missing.exe", 60));
            Throws<InvalidDataException>(() => store.SetGameTime(store.Today.Date, "one.exe", -1));
            Throws<InvalidDataException>(() => store.SetGameTime(store.Today.Date, "one.exe", double.NaN));
            Near(300, store.Today.TotalSeconds);
            DateOnly yesterday = store.Today.Date;
            store.EnsureDate(yesterday.AddDays(1));
            Throws<InvalidDataException>(() => store.SetGameTime(yesterday, "one.exe", null));
            Throws<InvalidDataException>(() => store.SetGameTime(yesterday, "one.exe", 900));
            Near(0, store.Today.TotalSeconds);
        });
        Check("tracking resumes from corrected values without restoring old row time", () =>
        {
            var rig = new Rig("correction-tracking") { Games = ["one.exe"] };
            rig.Poll();
            rig.Advance(30);
            rig.Poll();
            rig.Store.SetGameTime(rig.Store.Today.Date, "one.exe", 90);
            rig.Advance(30);
            rig.Poll();
            Near(120, rig.Store.Today.TotalSeconds);
            Near(120, rig.Store.Today.GameSeconds["one.exe"]);
            rig.Store.SetGameTime(rig.Store.Today.Date, "one.exe", 10);
            rig.Advance(30);
            rig.Poll();
            Near(40, rig.Store.Today.TotalSeconds);
            rig.Store.SetGameTime(rig.Store.Today.Date, "one.exe", null);
            rig.Advance(30);
            rig.Poll();
            Near(30, rig.Store.Today.GameSeconds["one.exe"]);
            Near(30, rig.Store.Today.TotalSeconds);
        });
    }

    private static void StatsEditors()
    {
        string path = Path.Combine(_directory, "stats-ui.json");
        var store = new TimeStore(path, new DateOnly(2026, 9, 13));
        store.Add(["one.exe"], 300);
        SettingsForm? window = null;
        using var form = new SettingsForm(new AppSettings(), _directory, _ => true, _ => { },
            (date, game, seconds) =>
            {
                store.SetGameTime(date, game, seconds);
                window!.UpdateToday(store.Today, "Проверка ручного редактирования");
            });
        window = form;
        form.UpdateToday(store.Today, "Проверка ручного редактирования");
        form.Show();
        var controls = Descendants(form).ToList();
        controls.OfType<TabControl>().Single().SelectedIndex = 2;
        Application.DoEvents();
        var list = controls.OfType<ListView>().Single();
        list.Items[0].Selected = true;
        form.UpdateToday(store.Today, "Обновление без потери выбранной строки");
        Equal(1, list.SelectedItems.Count);
        SaveThroughEditor(controls.OfType<Button>().Single(button => button.Text == "Изменить время…"), 15);
        Near(900, store.Today.GameSeconds["one.exe"]);
        Near(900, store.Today.TotalSeconds);
        True(controls.OfType<Label>().Any(label => label.Text == "Сегодня  00:15"));
        True(!controls.OfType<Button>().Any(button => button.Text == "Изменить общий итог…"));
        Near(900, new TimeStore(path, store.Today.Date).Today.GameSeconds["one.exe"]);
        Capture(form, "stats-edited.png");
    }

    private static void SaveThroughEditor(Button button, int minutes)
    {
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        bool completed = false;
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<EditTimeForm>().Single();
            var controls = Descendants(dialog).ToList();
            controls.OfType<NumericUpDown>().Single(number => number.Maximum >= 999).Value = 0;
            controls.OfType<NumericUpDown>().Single(number => number.Maximum == 59).Value = minutes;
            completed = true;
            timer.Stop();
            controls.OfType<Button>().Single(control => control.Text == "Сохранить").PerformClick();
        };
        timer.Start();
        button.PerformClick();
        True(completed);
    }

    private static void UiTests()
    {
        Check("negative monitor coordinates and oversized offsets", () =>
        {
            var settings = new AppSettings { Corner = OverlayCorner.BottomRight, OffsetX = 20, OffsetY = 30 };
            var location = OverlayForm.CalculateLocation(new Rectangle(-1920, 0, 1920, 1080), new Size(300, 90),
                settings, 1);
            Equal(new Point(-320, 960), location);
            settings.OffsetX = settings.OffsetY = 10000;
            location = OverlayForm.CalculateLocation(new Rectangle(-1920, 0, 1920, 1080), new Size(300, 90),
                settings, 1);
            Equal(new Point(-1920, 0), location);
        });
        Check("native running process detection", () =>
        {
            string name = Path.GetFileName(Environment.ProcessPath!);
            var settings = new AppSettings { Games = [name], Mode = TrackingMode.Running };
            True(GameDetector.Detect(settings).Games.Contains(name));
            settings.Mode = TrackingMode.Foreground;
            var snapshot = GameDetector.Detect(settings);
            True(snapshot.HasRunningGame);
            if (NativeMethods.ForegroundName(NativeMethods.GetForegroundWindow()) != name)
                Equal(0, snapshot.Games.Count);
            var clock1 = NativeMethods.ReadClock();
            var clock2 = NativeMethods.ReadClock();
            True(clock2.AwakeTicks >= clock1.AwakeTicks);
        });
        Check("settings controls and rendered pages", () =>
        {
            AppSettings? saved = null;
            string path = Path.Combine(_directory, "applied-settings.json");
            using var form = new SettingsForm(new AppSettings(), _directory, value =>
            {
                AtomicJson.Write(path, value);
                saved = value;
                return true;
            }, _ => { }, (_, _, _) => { });
            var today = new DailyStats
            {
                Date = new DateOnly(2026, 9, 13), TotalSeconds = 5820,
                GameSeconds = new() { ["Darktide.exe"] = 4320, ["bf6.exe"] = 1500 }
            };
            form.UpdateToday(today,
                "Учитываются: Darktide.exe · тестовый пример");
            form.Show();
            Application.DoEvents();
            Equal(Path.GetFileName(Environment.ProcessPath!), NativeMethods.ForegroundName(form.Handle));
            var controls = Descendants(form).ToList();
            var onlyWithGame = controls.OfType<CheckBox>().Single(check => check.Text == "Только при запущенной игре");
            True(onlyWithGame.Checked);
            var input = controls.OfType<TextBox>().Single(text => text.PlaceholderText.Length > 0);
            input.Text = "Darktide.exe";
            controls.OfType<Button>().Single(button => button.Text == "Добавить").PerformClick();
            var apply = controls.OfType<Button>().Single(button => button.Text == "Применить");
            apply.PerformClick();
            True(form.Visible);
            Equal(0, saved!.LimitMinutes);
            onlyWithGame.Checked = false;
            apply.PerformClick();
            True(!saved!.OverlayOnlyWithGame);
            var useLimit = controls.OfType<CheckBox>().Single(check => check.Text == "Включить");
            var limit = controls.OfType<NumericUpDown>().Single(number => number.Maximum == 1440);
            True(!limit.Enabled);
            useLimit.Checked = true;
            True(limit.Enabled);
            limit.Value = 90;
            apply.PerformClick();
            True(form.Visible);
            var restored = AtomicJson.Read(path, () => new AppSettings(), value => value.Validate(), out _);
            Equal(90, restored.LimitMinutes);
            useLimit.Checked = false;
            apply.PerformClick();
            Equal(0, saved!.LimitMinutes);
            var tabs = controls.OfType<TabControl>().Single();
            for (int index = 0; index < tabs.TabCount; index++)
            {
                tabs.SelectedIndex = index;
                Application.DoEvents();
                Capture(form, "settings-" + index + ".png");
            }
            tabs.SelectedIndex = 0;
            nint foreground = NativeMethods.GetForegroundWindow();
            if (foreground == form.Handle)
            {
                string name = Path.GetFileName(Environment.ProcessPath!);
                var settings = new AppSettings { Games = [name], Mode = TrackingMode.Foreground };
                True(GameDetector.Detect(settings).Games.Contains(name));
            }
            else
            {
                Console.WriteLine("SKIP: foreground game assertion; Windows did not activate the test form.");
            }
            controls.OfType<Button>().Single(button => button.Text == "Сохранить и свернуть").PerformClick();
            True(saved?.Games.SequenceEqual(["Darktide.exe"]) == true);
        });
        Check("time editor preserves unchanged seconds and accepts hours and minutes", () =>
        {
            using var dialog = new EditTimeForm("Darktide.exe", 3599.5);
            Near(3599.5, dialog.Seconds);
            var numbers = Descendants(dialog).OfType<NumericUpDown>().ToList();
            numbers.Single(number => number.Maximum >= 999).Value = 1;
            numbers.Single(number => number.Maximum == 59).Value = 15;
            Near(4500, dialog.Seconds);
            dialog.Show();
            Application.DoEvents();
            Capture(dialog, "edit-time.png");
        });
        Check("game input row and common footer fit at minimum width and after scaling", () =>
        {
            using var form = new SettingsForm(new AppSettings(), _directory, _ => true, _ => { }, (_, _, _) => { });
            form.Show();
            form.Width = form.MinimumSize.Width;
            foreach (float scale in new[] { 1f, 1.25f })
            {
                form.Scale(new SizeF(scale, scale));
                Application.DoEvents();
                var controls = Descendants(form).ToList();
                var input = controls.OfType<TextBox>().Single(text => text.PlaceholderText.Length > 0);
                Control row = input.Parent!;
                Control page = row.Parent!.Parent!;
                Rectangle rowBounds = page.RectangleToClient(row.RectangleToScreen(row.ClientRectangle));
                True(page.ClientRectangle.Contains(rowBounds));
                foreach (Control control in row.Controls)
                {
                    True(control.Bottom + control.Margin.Bottom <= row.ClientSize.Height);
                    True(control.Right + control.Margin.Right <= row.ClientSize.Width);
                    True(Math.Abs(control.Top + control.Height / 2 - input.Top - input.Height / 2) <= 1);
                    if (control is Button)
                        True(control.Height >= control.GetPreferredSize(Size.Empty).Height);
                }
                var link = controls.OfType<LinkLabel>().Single();
                True(link.Parent is not TabPage);
                True(link.Right + link.Margin.Right <= link.Parent!.ClientSize.Width);
                Capture(form, scale == 1 ? "settings-minimum.png" : "settings-scaled.png");
            }
        });
        Check("stats selection survives refresh and editing updates displayed total", StatsEditors);
        Check("overlay visibility without a game follows its setting", () =>
        {
            foreach (bool onlyWithGame in new[] { true, false })
            {
                string directory = Path.Combine(_directory, "visibility-" + onlyWithGame);
                AtomicJson.Write(Path.Combine(directory, "settings.json"), new AppSettings
                {
                    OverlayOnlyWithGame = onlyWithGame
                });
                using var context = new GameTimeContext(directory, startInTray: true);
                Application.DoEvents();
                bool visible = Application.OpenForms.OfType<OverlayForm>().Any(form => form.Visible);
                Equal(!onlyWithGame, visible);
            }
        });
        Check("overlay has click-through styles and does not steal focus", () =>
        {
            nint foreground = NativeMethods.GetForegroundWindow();
            using var overlay = new OverlayForm();
            var settings = new AppSettings { LimitMinutes = 120, PulseAt100 = false };
            foreach (int seconds in new[] { 3300, 5820, 8220 })
            {
                overlay.Present(settings, seconds, 0, true);
                Application.DoEvents();
                Equal(foreground, NativeMethods.GetForegroundWindow());
                int styles = GetWindowLong(overlay.Handle, -20);
                Equal(NativeMethods.OverlayStyles, styles & NativeMethods.OverlayStyles);
                Capture(overlay, "overlay-" + seconds + ".png");
                using var bitmap = new Bitmap(overlay.Width, overlay.Height);
                overlay.DrawToBitmap(bitmap, overlay.ClientRectangle);
                Equal(overlay.BackColor.ToArgb(), bitmap.GetPixel(0, overlay.Height - 1).ToArgb());
            }
            settings.Monitor = "disconnected-monitor";
            overlay.Present(settings, 8220, 0, true);
            True(Screen.PrimaryScreen!.WorkingArea.Contains(overlay.Bounds));
            settings.PulseAt100 = true;
            overlay.Present(settings, 8220, 0, true);
            Pump(1100);
            True(overlay.Opacity > 0 && overlay.Opacity < settings.OpacityPercent / 100.0);
            int withLimitWidth = overlay.Width;
            settings.LimitMinutes = 0;
            overlay.Present(settings, 5820, 0, true);
            Pump(1100);
            Near(settings.OpacityPercent / 100.0, overlay.Opacity);
            True(overlay.Width < withLimitWidth);
            True(overlay.Height < overlay.Font.Height * 2);
            Capture(overlay, "overlay-no-limit.png");
        });
    }

    private static void LiveMinute()
    {
        string directory = Path.Combine(_directory, "live");
        var settings = new AppSettings { Games = [Path.GetFileName(Environment.ProcessPath!)], LimitMinutes = 1 };
        AtomicJson.Write(Path.Combine(directory, "settings.json"), settings);
        using var context = new GameTimeContext(directory, startInTray: true);
        using var stop = new System.Windows.Forms.Timer { Interval = 63_000 };
        var process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        var elapsed = Stopwatch.StartNew();
        stop.Tick += (_, _) => context.ExitThread();
        stop.Start();
        Application.Run(context);
        process.Refresh();
        double cpu = (process.TotalProcessorTime - cpuBefore).TotalSeconds / elapsed.Elapsed.TotalSeconds * 100;
        double memory = process.WorkingSet64 / 1048576.0;
        Console.WriteLine($"LIVE: CPU {cpu:F3}% of one logical CPU; working set {memory:F1} MiB");
        var store = new TimeStore(Path.Combine(directory, "today.json"), DateOnly.FromDateTime(DateTime.Now));
        True(store.Today.TotalSeconds is >= 59 and <= 65);
    }

    private static void SingleInstance(string exe)
    {
        using var mutex = new Mutex(true, GameTime.Program.MutexName, out bool created);
        True(created);
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(exe), "--tray")
        {
            UseShellExecute = false, CreateNoWindow = true
        })!;
        True(process.WaitForExit(10000));
        Equal(0, process.ExitCode);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void Capture(Form form, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(Path.Combine(_directory, name), ImageFormat.Png);
    }

    private static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int second)
        => new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local));

    private static void Check(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { _failed++; Console.WriteLine("FAIL: " + name + "\n" + error); }
    }

    private static void True(bool value)
    {
        if (!value)
            throw new InvalidOperationException("Assertion failed.");
    }

    private static void Equal<T>(T expected, T actual) => True(EqualityComparer<T>.Default.Equals(expected, actual));

    private static void Near(double expected, double actual) => True(Math.Abs(expected - actual) < 0.01);

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private sealed class Rig
    {
        public DateTimeOffset Wall = Local(2026, 9, 13, 12, 0, 0);
        public ulong Ticks = 1_000_000_000;
        public HashSet<string> Games = [];
        public AppSettings Settings = new();
        public TimeStore Store;
        public GameTracker Tracker;

        public Rig(string name)
        {
            Store = new TimeStore(Path.Combine(_directory, name + ".json"), DateOnly.FromDateTime(Wall.DateTime));
            Tracker = new GameTracker(Store, () => new ClockSample(Wall, Ticks),
                _ => new GameSnapshot(new HashSet<string>(Games, StringComparer.OrdinalIgnoreCase), 0));
        }

        public void Poll() => Tracker.Poll(Settings, false);

        public void Advance(int seconds)
        {
            Wall = Wall.AddSeconds(seconds);
            Ticks += (ulong)seconds * 10_000_000;
        }
    }
}
