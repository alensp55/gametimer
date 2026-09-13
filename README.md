**English** | [Русский](README.ru.md)

# GameTime — preview version 0.3.2

A small local game timer for Windows 10/11 x64. It shows how much you have played today
and gently warns you when you reach your daily target. It never closes or blocks games.
The application interface is currently in Russian.

## Run the app

Download `GameTime.exe` from the [release page](https://github.com/alensp55/gametimer/releases/tag/v0.3.2)
and run it. The published self-contained version does not require a separate .NET installation.
When building locally, the executable is in `artifacts\win-x64\GameTime.exe`.

1. Add game `.exe` names manually or use **Choose .exe** (`Выбрать .exe`).
2. Choose a tracking mode. Optionally enable a daily target and set it in minutes.
3. On the **Timer window** (`Окно таймера`) tab, choose a monitor, corner, offsets and opacity.
   **Preview for 15 seconds** (`Предпросмотр на 15 секунд`) shows the timer even without a running game.
4. **Apply** (`Применить`) saves the settings and keeps the settings window open.
   **Save and minimize** (`Сохранить и свернуть`) also closes the settings window; the tray icon remains.
5. Enable **Start with Windows** (`Запускать вместе с Windows`) and save to turn on startup at sign-in.
   At sign-in, the app starts directly in the tray, even if the settings file is missing.
   A manual launch without `--tray` opens the settings window.

Double-click the tray icon to open settings. Its context menu includes pause and exit.
Closing the settings window with its X button discards unsaved form changes and keeps tracking in the background.
Launching the app again does not create a second instance in the same Windows user session.

## How time is counted

- **While the game .exe is running** (`Пока игровой .exe запущен`) includes background time, menus and matchmaking.
- **Only while the game window is active** (`Только когда окно игры активно`) counts a listed foreground process.
- Executable names are matched without regard to case. Full paths are not stored.
- Processes are checked every 60 seconds, with additional checks at startup, after settings changes and system events.
- Each interval is attributed to the state seen at the previous check. This is an approximation:
  short sessions may be missed, and closing a game may add up to an extra partial minute.
  Frequent switching can accumulate error. This is not a precise activity stopwatch.
- When several games run at once, each game's time increases, but the daily total increases only once per interval.
- The display shows whole hours and minutes. JSON stores seconds, including fractional seconds.
- Sleep, hibernation, a locked or disconnected user session, and manual pauses are excluded.
- The app does not reconstruct gaming time from periods when it was not running.

The day follows the local Windows date. At midnight, the daily data is replaced with a fresh set of counters.
The date is also checked at startup. There is no separate history of past days.
Changing the clock or time zone does not create gaming time: the interval spanning the change is skipped.
A change to another calendar date starts a new counter, including when the date is manually moved backward.
Long gaps without observation, over 90 seconds between checks, are also skipped.

On the **Today** (`Сегодня`) tab, you can edit a game's time or delete its row.
Double-clicking a row also opens the hours-and-minutes editor; Delete removes a row after confirmation.
Editing or deleting a row recalculates the daily total.
Changes are saved immediately, without **Apply**; deleting the last row resets the total to zero.
For games played sequentially, the total changes by the difference between the row's old and new time.
Exact overlaps between simultaneous games are not stored: the adjusted total is bounded by
the longest remaining game's time and the sum of the remaining rows.
If a game is still running, tracking continues and its deleted row reappears when more time is recorded.

## Timer

The timer displays hours and minutes only: `01:37`, without a heading or progress bar.
The window fits the text with a small amount of padding.
With a target enabled, it shows `01:37 / 02:00`; after exceeding it, `02:17 / 02:00 +17 мин`.
At the target, the text turns pink and can optionally pulse gently over a four-second cycle.
There is no 80% warning. With no target, there is no highlighting or pulsing.
The target can be 1–1440 minutes and is off in new settings. A JSON value of 0 means it is disabled.
An existing saved target stays enabled after an update; clear **Enable** (`Включить`) to turn it off.
There are no sounds or game blocks.

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

Data is stored in `%LocalAppData%\GameTime`:

- `settings.json` — application settings;
- `today.json` — the current date, daily total and individual game times;
- `.bak` — the previous saved copy for recovery, not an archive of past days.

Changed statistics are saved at each check, on pause and on a normal exit.
Writes use a temporary file, a flush to disk and an atomic file replacement.
A crash may lose the last unsaved interval, usually up to a minute;
absolute data preservation cannot be guaranteed after a power loss or disk damage.
If the main copy is damaged, the app tries the backup and displays a warning.
The damaged file is retained with a `.damaged-...` suffix. If neither copy is valid,
the app reports an error instead of silently resetting the data. Write errors appear in the tray and settings.

The app has no network requests, telemetry, updates, accounts, services or drivers.
It does not use DLL injection, graphics or input hooks, or read or modify game memory.
WinAPI queries the foreground process name using `PROCESS_QUERY_LIMITED_INFORMATION`;
standard .NET process-name enumeration is used to detect running games.
Inaccessible or exited processes are skipped. The app does not request elevated privileges.

Startup at sign-in uses the `GameTime` value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
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
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64
```

Output: `artifacts\win-x64\GameTime.exe`. Debug symbols are embedded and trimming is disabled.
The .NET runtime is bundled; its native files may be extracted to `%TEMP%\.net` on the first launch.

Checks without a third-party test framework:

```powershell
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release -- --live
dotnet run --project .\tests\GameTime.Tests\GameTime.Tests.csproj -c Release -- `
  --exe .\artifacts\win-x64\GameTime.exe
```

`--live` also runs a real one-minute tracking cycle, taking about 63 seconds.
`--exe` checks that the supplied executable exits on a duplicate launch before accessing the data.
The main application must be closed for that check.
Tests use their own files in `artifacts\tests` and do not change startup registration or user statistics.
UI tests briefly open test windows and save images of the three tabs and timer states.
A foreground check that cannot run on an isolated desktop is explicitly marked `SKIP`.

## Project structure

One WinForms project and a small executable test project.
`GameTracker` accumulates time from observations; `TimeStore` stores daily totals;
`AtomicJson` handles writes and recovery; `GameDetector` and `NativeMethods` identify games;
`OverlayForm`, `SettingsForm` and `EditTimeForm` provide the interface; `TrayManager` manages the tray;
`GameTimeContext` connects the application lifecycle, timers and Windows events;
`StartupRegistration` enables startup only when the user chooses it.

Results of checks actually performed are recorded in `VERIFICATION.md` (in Russian).

The main window footer shows `(c) alensp55@gmail.com` and `https://github.com/alensp55/gametimer`.
The link opens a browser only when clicked; the app itself does not make network requests.
