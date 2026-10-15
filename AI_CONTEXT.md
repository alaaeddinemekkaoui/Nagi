# Nagi engineering context

Native Windows 11 x64, C#, .NET 10, WinUI 3 / Windows App SDK 1.8. No cloud, web renderer, screenshots, keystroke recording or user activity telemetry. Preserve these constraints.

`Pulse.Core` has no Windows or WinUI dependencies. Keep rule priority/debounce and action validation testable there. `Pulse.Windows` implements real Windows APIs. `Pulse.Infrastructure` owns recoverable JSON and bounded logs. `Pulse.App` composes one shared state and one reusable Nagi window.

Lua UI uses MoonSharp in `Pulse.Infrastructure/LuaUiService.cs` and a declarative, validated appearance table. `Pulse.App/LuaUi.cs` owns its editor, reload and resource application. Keep execution off the dispatcher with a source/instruction budget; do not expose CLR, IO or non-preemptible callback libraries. The last working appearance survives invalid reloads. High Contrast, native docking/tray/hotkeys and genuine metrics remain controlled by the host. See `docs/LUA-UI.md`.

Build/test/package commands are in README and scripts. Do not claim validation without running commands. Never substitute simulated system or media data. Unsupported integrations must stay explicit. Manual profile locks always outrank rules. Quick actions require deliberate clicks and validation both when configured and executed. Background sampling must remain cancellable and outside the dispatcher thread.

Audio is a separate 50 ms cancellable loop with a cached, locked Core Audio endpoint/meter and bounded dispatcher work. Keep sampling and setters off the UI thread, dispose once, and never substitute fabricated peaks. Metering reads peak values without PCM recording. Akeno is a host palette compatible with Lua overrides and system High Contrast.

Quick search uses one separate window, a third conflict-safe hotkey, Windows shell application catalog and bounded local filename index. Keep indexing/search on workers, ignore stale generations, cancel/serialize refreshes, and enforce root scope on cached/current results. Never evaluate typed commands, read file contents, retain query history, or auto-send queries to a browser. See docs/QUICK-SEARCH.md.

1.7 rendering uses WinUI + Win2D fills and DWM corners, not native polygon regions. Preserve the vertical rail and inward preview placement. Integrations: PDH GPU engines (busiest aggregated physical engine), Core Audio communications-mic mute, existing WMI sensor providers, optional bundled PresentMon (no input events, unique owned sessions, no auto elevation), and local authenticated OBS v5. Do not label an acknowledged OBS command as successful until output state changes. Never fabricate thermal/FPS readings or install a kernel driver automatically. Keep OBS passwords outside settings/logs with per-user DPAPI. Startup/install/signing and physical coverage are documented separately.
