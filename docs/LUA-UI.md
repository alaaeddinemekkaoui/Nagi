# Lua UI studio

For a first script, use the [simple commented example](examples/simple-ui.lua). Paste its contents into the editor and select **Apply script**. It changes the accent to red, softens card corners and renames the CPU metric. Lua comments start with `--`; changing a comment has no effect on the appearance. For the rest of the app, see the [app guide](USER-GUIDE.md).

Open **Settings → Lua UI studio → Edit Lua UI**. Choose a preset or edit the source, then select **Apply script**. This validates and saves your script, enables Lua UI, and updates Nagi immediately. **Cancel** discards editor changes. Presets replace the editor text; saving remains deliberate.

The saved file is `%LOCALAPPDATA%\Pulse\scripts\ui.lua`. Edit it with any text editor and select **Reload Lua UI** in Settings. Changes to Windows/app theme or Nagi profile also reapply the script. The previous file is preserved as `ui.lua.bak` on a successful editor save. A missing file uses the Rose glass example without writing over anything.

Disable **Enable Lua UI** to restore the built-in appearance. Invalid scripts report their error and keep the last appearance that worked during the current session. At startup, an invalid saved script falls back to the built-in appearance. Reload is explicit; no file watcher or per-frame script execution is used.

```lua
local light = nagi.theme == "Light"
return {
  name = "Glacier",
  accent = light and "#006C8C" or "#78DFFF",
  surface = light and "#A8F0FAFF" or "#50253848",
  card_radius = 22,
  spacing = 14,
  labels = { cpu = "CPU activity", memory = "Memory" },
  sections = {
    metrics = true, network = true,
    media = nagi.profile ~= "Focus", audio = true
  }
}
```

| Option | Values | Default |
| --- | --- | --- |
| `name` | 1–40 characters | Custom |
| `accent` | Opaque `#RRGGBB` | Built-in theme accent |
| `surface` | `#RRGGBB` or `#AARRGGBB` surface/card fill | Built-in theme surface |
| `card_radius` | 8–28 device-independent pixels | 18 |
| `spacing` | 6–20 device-independent pixels between overview sections and metric columns | 10 |
| `labels.cpu`, `labels.memory` | 1–24 characters, no control characters | Processor, Memory |
| `sections.metrics`, `.network`, `.media`, `.audio` | Boolean overview section visibility | All true |

All options are optional. `return {}` gives the default appearance. Unknown options and invalid values are errors, rather than silently ignored typos. Native glass remains controlled by the Translucent glass preference. Windows High Contrast colors remain authoritative. The existing Show media controls preference also takes priority over `sections.media`. Section visibility affects the overview; compact indicators and hover previews remain available.

`nagi.theme` is `Dark`, `Light` or `Akeno`; System theme resolves to `Dark` or `Light`. `nagi.profile` is `Work`, `Gaming` or `Focus`. A fresh Lua environment is created for each evaluation. Ordinary Lua arithmetic, conditions, functions, numeric loops, table literals and the `math` library work. This is a declarative appearance API; scripts return data and do not create arbitrary WinUI controls or register event handlers.

The embedded [MoonSharp](https://www.moonsharp.org/) interpreter needs no separate Lua installation. Source files are limited to 32 KB of UTF-8 and execution to 20,000 Lua instructions, off the UI thread. IO, OS commands, CLR objects, module loading, debugging, metatables, string/table libraries and coroutine libraries are not exposed. The restricted libraries avoid non-preemptible Lua callbacks described in MoonSharp's [coroutine documentation](https://www.moonsharp.org/coroutines.html). These bounds support local customization; the runtime does not provide a separate process or an enforceable total heap quota.

The engine lives in `Pulse.Infrastructure/LuaUiService.cs`; `Pulse.App/LuaUi.cs` applies validated appearance settings. Tray controls, hotkeys, docking, metrics collection and media/audio operations stay in the host. MoonSharp's redistribution notice is in [MoonSharp-LICENSE.txt](MoonSharp-LICENSE.txt).

Host Akeno colors apply first; validated script accents/surfaces override them while enabled. A Lua script does not switch the host theme or change the native window shape. System High Contrast remains authoritative.

Nagi 1.8 exposes `nagi.theme` and `nagi.profile`. The earlier `pulse.theme` and `pulse.profile` names remain aliases to the same context, so existing scripts continue to work. The saved script path stays in the inherited Pulse data folder.
