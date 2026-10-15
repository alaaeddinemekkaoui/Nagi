# Nagi v1

<img src="docs/images/nagi-logo.png" width="96" alt="Nagi logo">

**Nagi (凪)** is a local-first Windows desktop companion: a slim glass rail at the screen edge, with system information, media controls, quick search and customizable panels.

`v1` is the first GitHub release. It contains the validated **Nagi 1.8.0** application build; the existing internal build number and development history are retained.

## Download and run

Download [Nagi-win-x64.zip](https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v1/Nagi-win-x64.zip) and its [SHA256 checksum](https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v1/Nagi-win-x64.zip.sha256) from the [v1 release](https://github.com/alaaeddinemekkaoui/Nagi/releases/tag/v1). Extract the entire ZIP, then open **Nagi.exe**. Keep the assets, DLLs and other files beside it.

- **Platform:** Windows 11 x64, version 22H2 or newer.
- **Runtime:** self-contained; no .NET SDK is needed to run the release.
- **Installation:** portable folder or optional per-user installer; administrator access is not required for the app.
- **Signing:** this release is unsigned.

Before extracting, open PowerShell in the ZIP's containing folder and verify the archive:

```powershell
Get-FileHash .\Nagi-win-x64.zip -Algorithm SHA256
Get-Content .\Nagi-win-x64.zip.sha256
```

Compare the hash values. To install after extraction:

```powershell
.\Install-Nagi.ps1
```

This creates `%LOCALAPPDATA%\Programs\Nagi` and a **Nagi** Start menu shortcut. Startup with Windows is opt-in under Settings. To uninstall:

```powershell
& "$env:LOCALAPPDATA\Programs\Nagi\Uninstall-Nagi.ps1"
```

Preferences and scripts are retained when uninstalling.

## Included in v1

- A left/right edge rail with hover previews, click-to-pin, inward-opening details, battery percentage and smooth transitions.
- The Nagi arc/ripple logo, Akeno Japanese black/red styling, dark/light/system themes, native glass and reduced-motion support.
- Real CPU, memory, network and supported GPU readings, with short activity graphs.
- Windows media artwork and supported playback controls; synchronized system output volume, mute and live signal activity.
- Default communications-microphone mute controls.
- **Alt+Space** search for installed apps, indexed filenames/folders, a calculator and Nagi actions.
- Work, Gaming and Focus profiles, foreground-application rules and configurable quick actions.
- Optional Lua appearance customization with a native editor, presets, reload and validation.
- Optional temperature-provider, PresentMon FPS and local OBS WebSocket integrations.
- Tray controls, configurable global shortcuts, a per-user installer and local diagnostic information.

## Everyday controls

| Control | Action |
| --- | --- |
| Hover the screen-edge tab | Reveal the rail |
| First click / second click | Pin the controls / expand details |
| Drag the logo | Adjust height or switch edges |
| **Alt + Space** | Quick search |
| **Ctrl + Alt + Space** | Expand/collapse the panel |
| **Ctrl + Alt + H** | Hide/show Nagi |
| **Escape** | Dismiss search or return toward the edge |
| Tray right-click → **Quit Nagi** | Exit the app |

Try `app: chrome`, `file: report`, `folder: projects`, `> volume` or `24 * 7` in search. File indexing covers selected folders, with a 40,000-entry cap; it does not read file contents.

## Simple Lua customization

Open **Settings → Lua UI studio → Edit Lua UI**, paste the following, then select **Apply script**:

```lua
-- Red accents with softer cards.
return {
  name = "Simple red",
  accent = "#FF4B4B", -- Highlight color.
  card_radius = 22,  -- Range: 8–28.
  spacing = 12      -- Range: 6–20.
}
```

Disable **Enable Lua UI** to restore the built-in appearance. Scripts can use `nagi.theme` and `nagi.profile`; the older `pulse` names remain compatible aliases. See the [commented example](docs/examples/simple-ui.lua) and [complete Lua reference](docs/LUA-UI.md).

## Integration requirements

| Feature | Requirement |
| --- | --- |
| GPU usage | Windows GPU Engine counters exposed by the installed driver |
| Temperatures | A running supported Libre Hardware Monitor / Open Hardware Monitor WMI provider |
| FPS | PresentMon tracing permission; use the separate helper if needed |
| Media artwork/transport | A player that publishes a supported Windows media session |
| OBS | OBS WebSocket v5 enabled locally, with matching port/password |

OBS recording changes require a deliberate button click; Nagi does not start a stream. OBS passwords are protected for the current Windows user. The microphone control targets the default communications input and does not record audio.

## Data and upgrades

Nagi was previously called Pulse / Halo. The existing `%LOCALAPPDATA%\Pulse` data folder is intentionally retained so settings, rules, Lua scripts, the filename index and protected credentials carry over. The executable, installer and Start menu entry use **Nagi**.

There is no account, backend or activity telemetry. Samples and activity history stay in memory. A web search opens in your browser only when selected.

## Validation and current limits

The release build passed with zero warnings/errors and **167 passing tests**. Live checks covered both rail edges, logo rendering, search, tray actions, installer migration, installed launch, graceful uninstall and preference retention.

GPU and microphone access were checked on the development machine. OBS status and recording were checked with an isolated empty-scene fixture. This machine had no temperature provider and denied unprivileged FPS tracing; no physical temperature or live game-FPS result is claimed. Physical multi-monitor/hotplug, actual sign-in, other media players and full accessibility coverage remain broader validation items.

## Documentation

- [Complete app guide](docs/USER-GUIDE.md)
- [Documentation index](docs/README.md)
- [Hardware and OBS setup](docs/INTEGRATIONS.md)
- [Build and packaging](docs/BUILD.md)
- [Validation record](docs/VALIDATION.md)
- [Changelog](CHANGELOG.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

The source retains its existing project names. To build from a checkout:

```powershell
dotnet restore Pulse.sln
dotnet build Pulse.sln -c Release
dotnet test Pulse.sln -c Release --no-build
.\scripts\Run-Nagi.ps1 -Configuration Release
```

Development requires the .NET 10 SDK and Windows SDK; see the build guide for details.
