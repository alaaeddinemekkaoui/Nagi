# Development build and debug menu

Open **Nagi-Dev-Portable.exe** for development. Open **Nagi-Portable.exe** for production. Both executables use the same project version; development tools are compiled out of production. Development opens its developer menu immediately and uses `%LOCALAPPDATA%\Nagi\Development` for settings, logs and reports. Its separate single-instance scope allows it to run beside production. An explicit `--data-dir <absolute-path>` overrides the development directory for isolated tests.

Press **F12** while Nagi has focus, right-click the dock → **Developer tools**, or choose **Settings → Developer tools** to return to the menu. Developer labels are in English; normal app pages retain the selected app language.

The menu provides:

- Direct links to Overview, Profiles and every Settings category, plus welcome setup and the dock introduction.
- Idle logo, ten-second timer, completion check and `00:00`, silent music visualizer, and a test notch notification. Clear preview stops the development timer and removes fixtures. Music and notification fixtures disappear after eight seconds; completion lasts five seconds. Timer tests replace the current development timer.
- All four screen edges, four themes and System/Media/OBS/Apps/Timer hover panels. The Timer panel starts a ten-second fixture if the Timer widget is hidden.
- Integration controls, a state snapshot with runtime sampling demand and process memory/CPU counters, application/error logs, an intentional test-error entry, and saved reports.
- An update-changelog dialog fixture with Update now and Later. It does not download or install an update. Production updates are disabled in Development so a stable release cannot replace the developer executable.

## Automated UI checks

Choose **Run automated UI checks**. Stop an existing countdown first. The runner opens each app/settings page, cycles Akeno/Light/Dark/System, checks timer/music/notification layout on every edge, verifies a real short timer completion and pause/resume, opens hover panels and exports diagnostics. It temporarily enables the Timer widget for its fixture and restores the saved development preferences afterward. It does not launch workspace apps, register startup, install updates, start OBS recording/streaming or replace the automated unit/integration suite.

Results are saved under the development data directory:

The runner also checks System/Custom visualizer colors across media views and antialiasing with glass enabled or disabled. Add `-MediaAppearance` to `Smoke-NagiDevelopment.ps1` to exercise the color picker and verify rendered bar colors and rounded corners from native captures. `-SkipUiChecks -MediaAppearance` runs just the focused UI smoke after the built-in suite has already passed.

| File | Purpose |
| --- | --- |
| `diagnostics/ui-checks.json` | Overall pass/fail and named checks with durations and failure details |
| `diagnostics/state.json` | Current page, dock geometry, profile, timer, providers, process counters and log paths |
| `logs/pulse.jsonl` | Application and developer activity |
| `logs/errors.jsonl` | Exceptions and deliberate test errors |

On a new development instance, command-line shortcuts open pages or run checks immediately:

```powershell
./artifacts/Nagi-Dev-win-x64/Nagi.exe --dev-page "Music"
./artifacts/Nagi-Dev-win-x64/Nagi.exe --dev-checks
./artifacts/Nagi-Dev-win-x64/Nagi.exe --dev-page "Appearance & dock" --data-dir C:\Nagi-Test
```

The portable forwards these arguments. Development-only arguments are not accepted by production. `--setup` still opens welcome setup.

## Building and verifying the pair

```powershell
./scripts/Publish-NagiVariants.ps1 -NoRestore
./scripts/Smoke-NagiDevelopment.ps1 -ExecutablePath ./artifacts/Nagi-Dev-win-x64/Nagi.exe -ProductionExecutablePath ./artifacts/Nagi-win-x64/Nagi.exe
./scripts/Smoke-NagiPortable.ps1 -ExecutablePath ./Nagi-Dev-Portable.exe
./scripts/Smoke-NagiPortable.ps1 -ExecutablePath ./Nagi-Portable.exe
```

The development smoke uses isolated settings, runs the built-in checks, captures the menu and a left-side notification, checks diagnostics/error logging, and verifies that the production menu and assembly exclude developer tools. Unit and Windows integration tests remain available with `dotnet test Pulse.sln -c Release`.

The UI runner reports actual observed assertions. It does not benchmark whole-app resource usage or verify physical monitor changes, real broadcasting, codecs or clean-machine installation. Broader checks remain in [Validation](VALIDATION.md).
