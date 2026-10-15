# Nagi

<img src="docs/images/nagi-logo.png" width="88" alt="Nagi logo">

A local-first Windows 11 desktop companion. **Nagi** reads system context and presents it through a vertical edge rail and control center. One process, one settings file, no account or backend. Nagi (凪) is named for calm; it was previously called Pulse / Halo.

Start with the [complete app guide](docs/USER-GUIDE.md), browse the [documentation index](docs/README.md), or try the [simple commented Lua example](docs/examples/simple-ui.lua). The guide covers installation, controls, every feature, settings, privacy, troubleshooting and removal.

**[Download Nagi v1](https://github.com/alaaeddinemekkaoui/Nagi/releases/tag/v1)** · [Release README](RELEASE_README.md). The `v1` release contains the tested Nagi 1.8.0 application build.

Customize Nagi in **Settings → Lua UI studio**. Edit and apply Lua for colors, card corners, spacing, metric labels and overview sections, or start from Rose glass, Glacier and Quiet focus presets. Saved scripts can be reloaded while Nagi runs. See [the Lua UI guide](docs/LUA-UI.md).

The vertical edge rail shows the Nagi logo, quick search, CPU/GPU, media, microphone mute and native battery percentage. Hover the battery for charging details; drag the logo to reposition Nagi. Systems without a battery show AC, and unavailable percentages stay explicit.

Akeno adds ink-black glass, vermilion accents and restrained Japanese motifs around the Nagi logo. Choose it under **Settings → Theme**. Media artwork appears in the vertical rail, hover preview and overview when supplied by the player. Output volume, mute and a measured signal-activity meter refresh independently every 50 ms; both volume sliders stay synchronized with Windows. Preview height adapts to screen work area and DPI.

Press **Alt + Space** for **Quick search**: installed apps, locally indexed filenames/folders, a calculator and Nagi actions. Try `app: chrome`, `file: report`, `> volume` or `24 * 7`. Configure folders and the shortcut under Settings → Quick search. A tray command and Nagi search icon are also available. [Quick search guide](docs/QUICK-SEARCH.md).

## Run

Prerequisites: Windows 11 x64 (22H2 or newer), .NET SDK **10.0.112** or a later 10.0.1xx patch, and the Windows 11 SDK. Visual Studio 2026 with Windows application development support is recommended for editing. The build restores the Windows App SDK and SDK BuildTools from NuGet. No Vercel tooling is used by this native app.

From the repository root in PowerShell:

```powershell
dotnet restore Pulse.sln
dotnet build Pulse.sln -c Release
dotnet test Pulse.sln -c Release --no-build
.\scripts\Run-Nagi.ps1 -Configuration Release
```

Or launch the built executable directly:

```powershell
& '.\src\Pulse.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\Nagi.exe'
```

Nagi starts as a small, borderless **tab connected to the right screen edge**. Hover to reveal the slim vertical rail. The first click keeps those controls open; another click expands the full panel. Moving away from an unpinned reveal returns it to the edge. Choose **Left edge** or **Right edge** in Settings. Drag the Nagi logo to change height or move to the other side. Older floating positions migrate to edge docking.

**Ctrl + Alt + Space** opens/collapses the full panel; **Ctrl + Alt + H** hides/shows Nagi. Both shortcuts are configurable. Escape returns it to the edge. Reopening Nagi reveals the running instance.

Inside the compact reveal, hover **CPU** for live CPU, memory and network details. Hover **Playing / Paused** for track information, artwork and supported playback controls; the same preview keeps output volume and mute available when no media session exists. Move into the preview to use it, or click **Show more** for the full panel. Hover keeps keyboard focus in your current app. Compact indicators follow the same first-click pin / second-click expand behavior.

Nagi also lives in the **system tray** (possibly under **Show Hidden Icons**). Left-click its waveform icon to open Nagi; right-click for Open, Hide, Settings, profiles and **Quit Nagi**. Right-clicking Nagi itself opens a matching quick menu. Hiding keeps Nagi running; Quit exits and removes the tray icon.

The first restore needs internet access. The built app and all core features work offline. The output bundles both .NET and the Windows App SDK runtime; keep the entire output folder together.

## Package and install

```powershell
.\scripts\Publish-Nagi.ps1
```

Produces `artifacts\Nagi-win-x64\Nagi.exe` and `artifacts\Nagi-win-x64.zip`. Extract the whole ZIP to use it portably. For a per-user installation, run `Install-Nagi.ps1` from the extracted folder. This copies the app to `%LOCALAPPDATA%\Programs\Nagi` and adds a Start menu shortcut. No administrator rights are needed. Run the installed `Uninstall-Nagi.ps1` to remove it; preferences are retained.

Startup with Windows is opt-in in Settings. The portable package is unsigned; it is not an MSIX or a Store submission. See [build details](docs/BUILD.md) and [validation](docs/VALIDATION.md).

## Features

- Reused topmost Nagi window with native tinted acrylic, layered glass cards, segmented navigation, compact media and audio controls, profile/rule editor and settings.
- Real CPU, physical memory, available memory and aggregate network throughput; short in-memory sparklines.
- Foreground process identity and exact executable/path rules with priorities and a 1.8-second debounce.
- Work, Gaming and Focus profiles. Manual choices lock; Automatic or the lock button releases the override. Focus collapses Nagi and suppresses nonessential notices.
- Windows media-session title, artist, artwork and supported transport controls. Empty/unsupported states are explicit.
- Default Windows audio endpoint name, output volume and mute using Core Audio.
- Validated shortcuts for apps, folders, files, HTTP(S), profiles and Nagi visibility; optional per-profile filtering.
- Akeno Japanese black/red, dark, light, system and high-contrast palettes; an opaque appearance option; display, anchor and drag offsets; outside-click and fullscreen preferences.
- Interruptible window springs, GPU composition fades/scales, staggered content and restrained hover/press feedback. Nagi and Windows reduced-motion settings disable these effects.
- Configurable hotkey with conflict recovery, per-user startup, single instance, cancellable background sampling and bounded local diagnostics.
- A separate hide/show hotkey and native tray menu, including recovery after Explorer recreates the notification area.
- Versioned, atomic JSON persistence with backups, field-level recovery and preservation of unknown fields. Future schemas are read-only.

GPU usage and microphone mute use native Windows APIs. Optional temperature sensors, PresentMon FPS and authenticated OBS status/recording controls are configured in Settings → Hardware & studio. Temperature readings need a WMI sensor provider; FPS tracing may need the separate helper with tracing permission. See [integration setup](docs/INTEGRATIONS.md). Nagi never fabricates readings or media. Network totals may include virtual adapters and may count traffic across more than one interface. Protected process paths can be unavailable. See [limitations and next milestones](docs/ROADMAP.md).

## Data and privacy

Configuration is `%LOCALAPPDATA%\Pulse\settings.json`. Recovery copies and `.bak` backups are kept beside it. Logs are bounded JSON lines in `%LOCALAPPDATA%\Pulse\logs`. Profile/activity history and metric samples stay in memory and disappear on exit. Nagi does not capture screenshots, record keystrokes, collect activity telemetry, or change Windows notification settings. Only the registered shortcuts are handled globally.

Shortcuts execute only on a user click. Shell scripts, command hosts, network paths and indirect shortcuts are blocked. Configuration is local; there is no configuration import/download feature. Revalidate paths at execution time.

## Source map

| Project | Responsibility |
| --- | --- |
| `src/Pulse.App` | WinUI 3, composition root, view model, Nagi, editors, design tokens |
| `src/Pulse.Core` | Models, contracts, rule evaluation, validation |
| `src/Pulse.Windows` | Win32, metrics, media, Core Audio, windows, hotkeys, startup |
| `src/Pulse.Infrastructure` | Settings, recovery, atomic writes, bounded diagnostic log |
| `tests/Pulse.Tests` | Platform-independent behavior and persistence tests |
| `tests/Pulse.Windows.Tests` | Live Windows integration and positioning/hotkey tests |

See [architecture](docs/ARCHITECTURE.md), [changelog](CHANGELOG.md), and [task status](TASKS.md).

The glass uses the real Windows desktop acrylic renderer. Windows can select an opaque fallback when Energy Saver, transparency settings, remote sessions or contrast policies require it; Nagi keeps those system preferences intact. Material state is shown in diagnostics. See [design references and motion details](docs/UI-DESIGN.md).
