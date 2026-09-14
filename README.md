**English** | [Русский](README.ru.md)

# OneMoreTimer — preview version 0.6.1

A small offline app timer for Windows 10/11 x64. It shows how much time you have spent in selected apps today
and gently warns you when you reach your daily target. Optional rules can force applications to close
at an individual daily limit or during scheduled intervals. These rules are off by default.
The interface supports English (default) and Russian. Choose a language in **General → Language** and click **Apply**.
The shared polling interval and startup are also in General. Tracking and closing rules are per app.

## Run the app

**[Download OneMoreTimer 0.6.1 for Windows x64 (.exe)](https://github.com/alensp55/OneMoreTimer/releases/download/v0.6.1/OneMoreTimer.exe)**

All versions are on the [release page](https://github.com/alensp55/OneMoreTimer/releases).
The locally built executable is in `artifacts\win-x64-0.6.1\OneMoreTimer.exe`.
The self-contained executable does not require a separate .NET installation.

1. Add game `.exe` names manually or use **Choose .exe** (`Выбрать .exe`).
2. Select an app and configure its tracking, optional daily limit and scheduled closing in the panel on the right.
   The daily target button above the list controls the overall timer. Set the polling interval in **General**.
3. On the **Timer window** (`Окно таймера`) tab, choose a monitor, corner, offsets and opacity.
   **Preview for 15 seconds** (`Предпросмотр на 15 секунд`) shows the timer even without a running game.
4. **Apply** (`Применить`) saves the settings and keeps the settings window open.
   **OK** (`ОК`) also closes the settings window; the tray icon remains.
5. Enable **Start with Windows** (`Запускать вместе с Windows`) and save to turn on startup at sign-in.
   At sign-in, the app starts directly in the tray, even if the settings file is missing.
   A manual launch without `--tray` opens the settings window.

Double-click the tray icon to open settings. Its context menu includes pause and exit.
Pause suspends both time tracking and forced termination; resuming immediately evaluates enabled rules.
Closing the settings window with its X button discards unsaved form changes and keeps tracking in the background.
Launching the app again does not create a second instance in the same Windows user session.

## How time is counted

- Each app can count only its active window, count while running (including background time), or not count at all.
  New apps default to active-window tracking. Existing apps keep their previous mode after migration.
- Executable names are matched without regard to case. Full paths are not stored.
- The polling interval is configurable from 1 to 3600 seconds, with a default of 60.
  Additional checks run at startup, after settings changes and system events.
- Each interval is attributed to the state seen at the previous check. This is an approximation:
  short sessions may be missed, and closing a game may add up to one extra polling interval.
  Frequent switching can accumulate error. This is not a precise activity stopwatch.
- When several games run at once, each game's time increases, but the daily total increases only once per interval.
- The display shows whole hours and minutes. JSON stores seconds, including fractional seconds.
- Sleep, hibernation, a locked or disconnected user session, and manual pauses are excluded.
- The app does not reconstruct gaming time from periods when it was not running.

The day follows the local Windows date. At midnight, the daily data is replaced with a fresh set of counters.
The date is also checked at startup. There is no separate history of past days.
Changing the clock or time zone does not create gaming time: the interval spanning the change is skipped.
A change to another calendar date starts a new counter, including when the date is manually moved backward.
Gaps longer than 1.5 times the polling interval (at least 5 seconds) are also skipped.

The **Apps** tab combines every application and today's statistics in one list.
Select a row to edit its rules in the panel on the right. The checkbox beside the app name enables or pauses
all its rules; it preserves settings and accumulated time. The Rules column summarizes its behavior.
Apps used only for scheduled closing can keep tracking off.

**Edit time** corrects today's app time. **Delete** removes the app, all its rules and its daily row.
These two actions save immediately and recalculate the overall daily total; ordinary rule changes wait for **Apply**.
For sequential use, the total changes by the difference between the old and new row values.
Exact overlaps are not stored: the adjusted total stays between the longest remaining app's time and their sum.
Deleting the last row resets the total to zero.

## App limits and schedules

- Enable **Daily limit**, enter hours and minutes (1 minute to 24 hours), and choose **Notify me** or **Terminate**.
  The limit uses this app's counted time, independently of the overall timer target.
  Notifications appear through the tray when the limit is reached; Windows notification settings affect visibility.
  Turning tracking off also disables the daily limit.
- **Scheduled closing** offers **Off**, **Shared schedule**, or **Custom schedule**.
  Click **Edit schedule** to set forbidden intervals. Editing the shared schedule affects every app using it;
  a custom schedule belongs only to the selected app. Scheduled closing works even with tracking off.
  Times follow the local Windows clock: start included, end excluded.
  Monday 22:00–07:00 ends on Tuesday morning; **All day** covers selected calendar days.
- Rules take effect on **Apply** or **OK**, including if the limit has already been reached.
  Tracking, daily limits and schedules share the polling interval from General.
  Restarted apps are closed again at a check while a closing rule applies. Daily limits reset at midnight.
- Old settings migrate automatically: the two app lists are merged, previous tracking modes and enabled limits
  are retained, and previously enabled scheduled apps use the shared schedule. Schedule-only apps do not start
  counting time. Previously disabled scheduled closing stays off.

Forced termination can lose unsaved data and interrupt a match. It acts on all matching executable names
in the current Windows session, including background processes; child processes with other names are not targeted.
The app itself and core Windows processes cannot be added as termination targets.
The app does not request administrator rights. Access failures appear in the tray and settings.
Exit OneMoreTimer or pause it through the tray to stop enforcing rules. This is a voluntary tool, not tamper-proof control.

## Timer

The timer displays hours and minutes only: `01:37`, without a heading or progress bar.
The window fits the text with a small amount of padding.
With a target enabled, it shows `01:37 / 02:00`; after exceeding it, `02:17 / 02:00 +17 мин`.
At the target, the text turns pink and can optionally pulse gently over a four-second cycle.
There is no 80% warning. With no target, there is no highlighting or pulsing.
The target can be 1–1440 minutes and is off in new settings. A JSON value of 0 means it is disabled.
An existing saved target stays enabled after an update; open **Edit daily target** and clear the target checkbox to turn it off.
There are no sounds. The overall timer target itself never terminates an application.

**Only while a game is running** (`Только при запущенной игре`) is enabled by default:
the timer appears when a game is detected and disappears at the first check that finds no game.
This checks whether a game is running even when tracking is set to foreground-only and the game is in the background.
Clear the checkbox to show the timer without a running game. Pausing, sleep and session locking hide the window.
Disabling the timer display does not stop tracking. The window does not take focus and lets clicks pass through.
Its position is changed in settings; dragging it during a game is not supported.

Windowed and borderless modes are the intended modes for displaying the timer over a game.
In true exclusive fullscreen, a separate Windows window may not appear over the game:
use a second monitor or borderless mode. The app does not interfere with the game's display mode.
**Game monitor (auto)** remembers the screen of the last detected foreground game.
Until a game has been observed, it uses the primary screen.
If the selected monitor is disconnected, the timer moves to the primary screen; the saved monitor choice is retained.
Reopen settings to refresh the list of available monitors after connecting a display.

## Local data and trust

Data remains in `%LocalAppData%\GameTime` for compatibility with earlier versions. The rename preserves settings
and today's statistics:

- `settings.json` — application settings;
- `today.json` — the current date, daily total and individual game times;
- `.bak` — the previous saved copy for recovery, not an archive of past days.

At frequent polling intervals, changed statistics are saved no more than once per minute during regular polling;
at intervals above a minute they are saved at each poll. Edits, pause and a normal exit save immediately.
Writes use a temporary file, a flush to disk and an atomic file replacement.
A crash may lose time since the last save, plus the current unsampled interval;
absolute data preservation cannot be guaranteed after a power loss or disk damage.
If the main copy is damaged, the app tries the backup and displays a warning.
The damaged file is retained with a `.damaged-...` suffix. If neither copy is valid,
the app reports an error instead of silently resetting the data. Write errors appear in the tray and settings.

The app has no network requests, telemetry, updates, accounts, services or drivers.
It does not use DLL injection, graphics or input hooks, or read or modify game memory.
WinAPI queries the foreground process name using `PROCESS_QUERY_LIMITED_INFORMATION`;
standard .NET process-name enumeration is used to detect running games.
Enabled termination rules additionally request `PROCESS_TERMINATE` and use standard WinAPI `TerminateProcess`.
The executable name is verified on the same process handle before termination.
Inaccessible or exited processes cannot be forcibly closed; the app does not request elevated privileges.

After updating from GameTime, exit the old version through its tray icon, launch OneMoreTimer and click **OK**.
If startup was enabled, its existing entry will be replaced with the new name and executable path.

Startup at sign-in uses the `OneMoreTimer` value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
It launches the current `.exe` with `--tray`. After moving the executable, disable and re-enable startup.
To uninstall, disable startup, exit through the tray and delete the `.exe`.
You can separately delete the data folder if you no longer need its settings and statistics.

This preview build is not signed with a publisher certificate.
The absence of antivirus false positives or compatibility with every anti-cheat cannot be guaranteed.
Do not disable protection to run the app. Check your particular games and display modes separately.

## Build from source

Requires Windows and the .NET 10 SDK. Neither the app nor the test project has third-party PackageReference entries.
For the first self-contained publish, the SDK downloads official .NET runtime packages from NuGet.
This happens during the build; the finished application does not make network requests.

From the repository root:

```powershell
dotnet build
dotnet run --project .\src\GameTime\GameTime.csproj
dotnet build -c Release
```

Publish one self-contained Windows x64 `.exe`:

```powershell
dotnet publish .\src\GameTime\GameTime.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64-0.6.1
```

Output: `artifacts\win-x64-0.6.1\OneMoreTimer.exe`. Debug symbols are embedded and trimming is disabled.
The .NET runtime is bundled; its native files may be extracted to `%TEMP%\.net` on the first launch.

Checks without a third-party test framework:

```powershell
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release -- --live
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release -- `
  --exe .\artifacts\win-x64-0.6.1\OneMoreTimer.exe
```

`--live` also runs a real one-minute tracking cycle, taking about 63 seconds.
`--exe` checks that the supplied executable exits on a duplicate launch before accessing the data.
The main application must be closed for that check.
Tests use their own files in `artifacts\tests` and do not change startup registration or user statistics.
Termination tests create disposable helper processes and only terminate those helpers.
UI tests briefly open test windows and save images of the three tabs and timer states.
A foreground check that cannot run on an isolated desktop is explicitly marked `SKIP`.

## Project structure

One WinForms project and a small executable test project.
`GameTracker` accumulates time from observations; `TimeStore` stores daily totals;
`AtomicJson` handles writes and recovery; `GameDetector` and `NativeMethods` identify games;
`OverlayForm`, `SettingsForm` and `EditTimeForm` provide the interface; `TrayManager` manages the tray;
`GameTimeContext` connects the application lifecycle, timers and Windows events;
`ProcessEnforcer` applies opt-in limits and schedules; `BlockingSettings` defines their configuration;
`ApplicationRulePanel`, `DurationInput`, `RuleForms` and `ScheduleEditorForm` provide the rule editors;
`StartupRegistration` enables startup only when the user chooses it.

Results of checks actually performed are recorded in `VERIFICATION.md` (in Russian).

The main window footer shows `(c) alensp55@gmail.com` and `https://github.com/alensp55/OneMoreTimer`.
The link opens a browser only when clicked; the app itself does not make network requests.

The footer support link opens [Buy me a coffee](https://buymeacoffee.com/alensp55) in English
or [CloudTips](https://pay.cloudtips.ru/p/ae5b2218) in Russian, only when clicked.
