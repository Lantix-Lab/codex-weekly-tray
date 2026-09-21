# Codex Weekly Tray

A small Windows tray application that shows the remaining Codex weekly usage allowance as a pie chart.

This is an unofficial community project. It is not affiliated with, endorsed by, or supported by OpenAI.

## Icon behavior

- The colored area is the remaining percentage.
- Usage removes the colored area clockwise, starting at 12 o'clock.
- More than 50 percent remaining is green.
- From 21 to 50 percent remaining is amber.
- From 0 to 20 percent remaining is red.
- The unused part of the circle is dark gray.
- An unavailable state is shown as a gray circle with a diagonal line.
- The icon contains no text or digits.

The menu and tooltip still show the exact percentage and reset time.

## Data source and privacy

The application reads `account/rateLimits/read` from the local `codex app-server` process and reuses the existing Codex login state.

- It does not scrape a web page.
- It does not read or store passwords.
- It does not store access tokens.
- It does not send data to third parties.

See the [OpenAI Codex App Server documentation](https://learn.chatgpt.com/docs/app-server) for the protocol.

## Usage impact

Running this tray application does not consume model-inference tokens or reduce the Codex weekly usage allowance. It only calls `account/rateLimits/read` to retrieve account metadata. It never starts a Codex thread or model turn and does not call `thread/start` or `turn/start`.

The 60-second refresh interval produces only small account-status requests and negligible local CPU, memory, and network activity. The Codex App Server may also perform its own lightweight metadata refreshes, but the tray application does not request model inference.

## ASCII policy

All repository text is restricted to 7-bit ASCII. The build runs `verify-ascii.ps1` and fails if any checked file contains a byte greater than `0x7F`. This avoids BOM and locale-dependent source encoding problems.

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

Verify the ASCII policy directly:

```powershell
.\verify-ascii.ps1
```

## Tray menu

- **Refresh now** reloads the usage allowance.
- **Open Codex usage page** opens the usage page in the default browser.
- **Start with Windows** adds or removes the current-user startup entry. It is disabled by default.
- **Exit** closes the application and its App Server child process.

The application refreshes every 60 seconds. Double-clicking the icon also refreshes it.

## Project layout

```text
src/CodexWeeklyTray/   Application source
tests/                 Offline tests and optional live integration test
.github/workflows/     GitHub Actions build
build.ps1              Local build entry point
test.ps1               Test entry point
verify-ascii.ps1       ASCII byte validation
```

## License

Released under the [MIT License](LICENSE).
