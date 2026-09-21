# Codex Weekly Tray

[English](README.md) | [Simplified Chinese](README.zh-CN.md)

A small Windows tray application that shows the remaining Codex weekly and 5-hour usage allowances as pie charts.

This is an unofficial community project. It is not affiliated with, endorsed by, or supported by OpenAI.

## Icon behavior

- Each available limit window has its own tray icon in the same application.
- The weekly window uses one icon. A second icon appears when a 5-hour window is returned.
- Seeing only one icon is expected when the account returns only one supported window.
- A blue outer ring identifies the weekly icon.
- A purple outer ring identifies the 5-hour icon.
- Windows controls tray icon order, so left-to-right position is not used for identification.
- The colored area is the remaining percentage.
- Usage removes the colored area clockwise, starting at 12 o'clock.
- More than 50 percent remaining is green.
- From 21 to 50 percent remaining is amber.
- From 0 to 20 percent remaining is red.
- The unused part of the circle is dark gray.
- An unavailable state is shown as a gray circle with a diagonal line.
- The icon contains no text or digits.

The inner sector color shows the remaining level; the outer ring color identifies the window type. Each icon's tooltip also identifies its window and shows the exact percentage and reset time. Both icons share one menu, refresh timer, App Server connection, and application process. A refresh makes one `account/rateLimits/read` request and updates every returned window; displaying two icons does not double the request rate.

## Supported windows

- A 300-minute window is displayed as the 5-hour icon.
- A 10,080-minute window is displayed as the weekly icon.
- If both windows are returned, both icons are visible.
- If only one supported window is returned, only its icon is visible.

Window types are detected from `windowDurationMins`, not inferred from the account plan name.

## Data source and privacy

The application reads `account/rateLimits/read` from the local `codex app-server` process and reuses the existing Codex login state.

- It does not scrape a web page.
- It does not read or store passwords.
- It does not store access tokens.
- It does not send data to third parties.

See the [OpenAI Codex App Server documentation](https://learn.chatgpt.com/docs/app-server) for the protocol.

## Usage impact

Running this tray application does not consume model-inference tokens or reduce either Codex usage allowance. It only calls `account/rateLimits/read` to retrieve account metadata. It never starts a Codex thread or model turn and does not call `thread/start` or `turn/start`.

The selectable refresh interval produces only small account-status requests and negligible local CPU, memory, and network activity. The Codex App Server may also perform its own lightweight metadata refreshes, but the tray application does not request model inference.

## Encoding policy

All source code, scripts, configuration files, and English documentation are restricted to 7-bit ASCII. Localized documentation may use UTF-8 without a byte-order mark. The build runs `verify-ascii.ps1` and fails when a file violates its declared encoding policy.

## Requirements

- Windows 10 or Windows 11.
- Codex Desktop or Codex CLI installed and signed in.
- .NET Framework 4.5 or later.

If `codex.exe` is not available through `PATH`, the application also checks the default Codex Desktop installation directory. The `CODEX_CLI_PATH` environment variable can provide an explicit full path.

## Build

Run in PowerShell:

```powershell
.\build.ps1
```

Output:

```text
dist\CodexWeeklyTray.exe
```

The build uses the Windows C# compiler and does not download NuGet packages. Offline tests run by default.

Exit a running copy from its tray menu before rebuilding, because Windows locks the executable while it is running.

## Distribution policy

This repository distributes source code only. It does not publish prebuilt executables through GitHub Releases or Actions artifacts. Users clone or download the repository and run `build.ps1` locally.

## Run and test

Start the application:

```powershell
.\dist\CodexWeeklyTray.exe
```

Run offline tests:

```powershell
.\test.ps1
```

Also test against the currently signed-in account:

```powershell
.\test.ps1 -Live
```

Verify the encoding policy directly:

```powershell
.\verify-ascii.ps1
```

## Tray menu

- **Refresh now** reloads the usage allowance.
- **Refresh interval** selects 1, 5, 15, or 30 minutes. The default is 1 minute, and the selection is saved for the current Windows user.
- **Open Codex usage page** opens the usage page in the default browser.
- **Start with Windows** adds or removes a shortcut in the current user's Windows Startup folder. It is disabled by default.
- **Exit** closes the application and its App Server child process.

Double-clicking either icon refreshes both icons immediately. Changing the interval also performs an immediate refresh and schedules later refreshes at the selected interval.

The selected interval is stored as the `RefreshIntervalMinutes` value under `HKCU\Software\CodexWeeklyTray`. No account credentials or usage history are stored there.

After at least one successful read, the first two consecutive refresh failures keep the last known chart and mark its menu text and tooltip as `stale`. A third consecutive failure changes the chart to the unavailable state. Any successful refresh clears the failure count immediately. If the application has never read a valid value, the first failure remains unavailable because there is no previous result to display.

The startup shortcut is stored as `CodexWeeklyTray.lnk` in the folder opened by `shell:startup`. Versions that used the current-user `Run` registry key are migrated automatically: the shortcut is created and verified before the legacy registry value is removed.

## Project layout

```text
src/CodexWeeklyTray/   Application source
tests/                 Offline tests and optional live integration test
.github/workflows/     GitHub Actions build
README.zh-CN.md        Simplified Chinese documentation
build.ps1              Local build entry point
test.ps1               Test entry point
verify-ascii.ps1       ASCII and UTF-8 encoding validation
```

## License

Released under the [MIT License](LICENSE).
