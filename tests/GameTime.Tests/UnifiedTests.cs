using System.Text.RegularExpressions;
using GameTime;

internal static partial class TestRunner
{
    private static void UnifiedTests()
    {
        Check("migration unifies old lists without enabling tracking or disabled restrictions", () =>
        {
            string path = Path.Combine(_directory, "legacy-unified.json");
            File.WriteAllText(path, """
                {"Games":["Darktide.exe"],"Mode":"Foreground","LimitMinutes":90,
                 "AppLimits":{"Darktide.exe":{"Track":true,"Terminate":true,"Minutes":120}},
                 "Schedule":{"Enabled":true,"Applications":["steam.exe","DARKTIDE.EXE"],
                 "Periods":[{"Days":62,"StartMinute":0,"EndMinute":960}]}}
                """);
            var settings = AtomicJson.Read(path, () => new AppSettings(), value => value.Validate(), out _);
            Equal(1, settings.FormatVersion);
            Equal(2, settings.Games.Count);
            True(settings.IsTracked("Darktide.exe"));
            True(!settings.IsTracked("steam.exe"));
            Equal(TrackingMode.Foreground, settings.TrackingFor("Darktide.exe"));
            Equal(AppScheduleMode.Shared, settings.AppLimits["steam.exe"].ScheduleMode);
            True(settings.AppLimits["Darktide.exe"].HasLimit);
            Equal(90, settings.LimitMinutes);
            Equal(0, settings.Schedule.Applications.Count);
            settings.Validate();
            AtomicJson.Write(path, settings);
            var restored = AtomicJson.Read(path, () => new AppSettings(), value => value.Validate(), out _);
            Equal(2, restored.Games.Count);
            Equal(120, restored.AppLimits["Darktide.exe"].Minutes);
            var disabled = new AppSettings
            {
                Schedule = new() { Applications = ["steam.exe"], Periods = [new()] }
            };
            disabled.Validate();
            True(!disabled.IsTracked("steam.exe"));
            Equal(AppScheduleMode.None, disabled.AppLimits["steam.exe"].ScheduleMode);
        });
        Check("each app has its own tracking mode and master switch", () =>
        {
            var settings = ExampleRules();
            var counted = GameDetector.SelectCounted(settings, ["active.exe", "running.exe", "schedule.exe"],
                "ACTIVE.EXE");
            True(counted.SetEquals(["active.exe", "running.exe"]));
            True(GameDetector.SelectCounted(settings, counted, "other.exe").SetEquals(["running.exe"]));
            settings.AppLimits["running.exe"].Enabled = false;
            Equal(0, GameDetector.SelectCounted(settings, counted, "other.exe").Count);
        });
        Check("notify, terminate, shared and custom schedules act independently", () =>
        {
            var settings = ExampleRules();
            var now = new DateTime(2026, 9, 14, 23, 0, 0);
            var today = new DailyStats
            {
                Date = DateOnly.FromDateTime(now),
                GameSeconds = new() { ["active.exe"] = 600, ["running.exe"] = 600 }
            };
            True(ProcessEnforcer.GetReachedLimits(settings, today, now, false).SetEquals(["active.exe"]));
            True(ProcessEnforcer.GetTargets(settings, today, now).SetEquals(["running.exe", "schedule.exe"]));
            settings.AppLimits["schedule.exe"].ScheduleMode = AppScheduleMode.Custom;
            settings.AppLimits["schedule.exe"].CustomPeriods =
                [new() { Days = 2, StartMinute = 10 * 60, EndMinute = 11 * 60 }];
            True(ProcessEnforcer.GetTargets(settings, today, now).SetEquals(["running.exe"]));
            settings.AppLimits["running.exe"].UseLimit = false;
            Equal(0, ProcessEnforcer.GetTargets(settings, today, now).Count);
            settings.AppLimits["active.exe"].Enabled = false;
            Equal(0, ProcessEnforcer.GetReachedLimits(settings, today, now, false).Count);
            var schedule = settings.AppLimits["schedule.exe"];
            schedule.ScheduleMode = AppScheduleMode.Shared;
            True(ProcessEnforcer.GetTargets(settings, today, now.AddHours(7)).Contains("schedule.exe"));
            True(!ProcessEnforcer.GetTargets(settings, today, now.AddHours(8)).Contains("schedule.exe"));
            schedule.Enabled = false;
            Equal(0, ProcessEnforcer.GetTargets(settings, today, now).Count);
        });
        Check("duration fields preserve 24 hours and cannot create zero or negative limits", () =>
        {
            using var duration = new DurationInput();
            foreach (int minutes in new[] { 1, 30, 60, 120, 1439, 1440, 1 })
            {
                duration.Minutes = minutes;
                Equal(minutes, duration.Minutes);
            }
            using var positive = new IntegerInput { Minimum = 1, Maximum = 3600, Value = 60 };
            foreach (string invalid in new[] { "0", "-2", "3601", "abc", "1.5" })
            {
                positive.Text = invalid;
                Near(60, (double)positive.Value);
            }
        });
        Check("side panel edits, refresh, shared schedules and language preserve the same draft", UnifiedEditor);
    }

    private static AppSettings ExampleRules()
    {
        var settings = new AppSettings
        {
            FormatVersion = 1, Games = ["active.exe", "running.exe", "schedule.exe"],
            Schedule = new() { Periods = [new() { Days = 2 }] },
            AppLimits = new()
            {
                ["active.exe"] = new() { Mode = TrackingMode.Foreground, UseLimit = true, Minutes = 10 },
                ["running.exe"] = new()
                {
                    Mode = TrackingMode.Running, UseLimit = true, Minutes = 10, Terminate = true
                },
                ["schedule.exe"] = new() { Track = false, ScheduleMode = AppScheduleMode.Shared }
            }
        };
        settings.Validate();
        return settings;
    }

    private static void UnifiedEditor()
    {
        var settings = ExampleRules();
        settings.Language = AppLanguage.English;
        AppSettings? saved = null;
        using var form = new SettingsForm(settings, _directory, value =>
        {
            value.Validate();
            saved = value.Copy();
            return true;
        }, _ => { }, (_, _, _) => { });
        var today = new DailyStats
        {
            Date = DateOnly.FromDateTime(DateTime.Now), TotalSeconds = 300,
            GameSeconds = new() { ["active.exe"] = 180, ["running.exe"] = 120 }
        };
        form.UpdateToday(today, "");
        form.Show();
        Application.DoEvents();
        var controls = Descendants(form).ToList();
        var list = controls.OfType<ListView>().Single(control => control.Name == "Applications");
        var panel = controls.OfType<ApplicationRulePanel>().Single();
        var panelControls = Descendants(panel).ToList();
        var tracking = panelControls.OfType<ComboBox>().Single(control => control.Name == "AppTracking");
        var duration = panelControls.OfType<DurationInput>().Single();
        var useLimit = panelControls.OfType<CheckBox>().Single(control => control.Name == "UseAppLimit");
        tracking.SelectedIndex = 2;
        duration.Minutes = 75;
        form.UpdateToday(today, "");
        Equal(2, tracking.SelectedIndex);
        Equal(75, duration.Minutes);
        list.Items.Cast<ListViewItem>().Single(item => item.Text == "schedule.exe").Selected = true;
        Equal(0, tracking.SelectedIndex);
        True(!useLimit.Checked);
        list.Items.Cast<ListViewItem>().Single(item => item.Text == "active.exe").Selected = true;
        Equal(75, duration.Minutes);
        var apply = controls.OfType<Button>().Single(button => button.Text == "Apply");
        apply.PerformClick();
        Equal(75, saved!.AppLimits["active.exe"].Minutes);
        Equal(TrackingMode.Running, saved.TrackingFor("active.exe"));
        True(!saved.AppLimits["active.exe"].Terminate);
        var tabs = controls.OfType<TabControl>().Single();
        Equal(3, tabs.TabCount);
        var language = controls.OfType<ComboBox>().Single(combo => combo.Name == "Language");
        foreach (int index in new[] { 0, 1, 0 })
        {
            language.SelectedIndex = index;
            apply.PerformClick();
            Equal(75, saved!.AppLimits["active.exe"].Minutes);
            Equal(TrackingMode.Running, saved.TrackingFor("active.exe"));
            True(form.Text.StartsWith("OneMoreTimer", StringComparison.Ordinal));
            for (int tab = 0; tab < tabs.TabCount; tab++)
            {
                tabs.SelectedIndex = tab;
                Application.DoEvents();
                if (index == 0)
                    foreach (var control in Descendants(tabs.TabPages[tab]))
                        if (Regex.IsMatch(control.Text, "[А-Яа-яЁё]"))
                            throw new InvalidOperationException("Untranslated: " + control.Text);
                Capture(form, $"unified-{index}-{tab}.png");
            }
        }
        using var schedule = new ScheduleEditorForm(true, settings.Schedule.Periods);
        schedule.Show();
        Application.DoEvents();
        Equal(1320, schedule.Periods[0].StartMinute);
        Capture(schedule, "unified-schedule.png");
        using var target = new DailyTargetForm(120);
        target.Show();
        Application.DoEvents();
        Equal(120, target.Minutes);
        Capture(target, "unified-target.png");
    }
}
