# Nagi identity

**Nagi** (凪, “calm”) is the single product name from version 1.8 onward. Use **Nagi** in headings, the Start menu, window titles and tray commands. The earlier product names were Pulse and Halo; Akeno remains an appearance theme.

<img src="images/nagi-logo.png" width="160" alt="Nagi red arc and water-ripple logo">

## Logo

The mark combines a red open arc and a quiet water ripple. It has no lettering or background tile, so the same shape works in the rail, header, executable and Windows tray. The transparent PNG is the original generated artwork; Windows ICO assets contain 16, 20, 24, 32, 48, 64, 128 and 256 pixel versions. Keep clear space around it and retain its aspect ratio.

| Asset | Location in the source checkout |
| --- | --- |
| Original transparent logo | `src/Pulse.App/Assets/Nagi.Logo.png` |
| Application icon | `src/Pulse.App/Assets/Nagi.ico` |
| Tray icon | `src/Pulse.App/Assets/Nagi.Tray.ico` |
| Documentation copy | [images/nagi-logo.png](images/nagi-logo.png) |

In a portable release, app assets are under `Assets` beside `Nagi.exe`. The fixed red mark is the product identity; Lua accent colors continue to customize the surrounding controls.

## Generation record

Generated with the built-in image-generation tool on 2026-10-15, requesting a transparent background. The generated PNG was copied into the project; Windows icons were exported from it without changing the design.

Prompt:

> Create a final minimalist app logo symbol for Nagi, a Japanese-inspired Windows desktop companion named for calm. One single iconic abstract mark: a smooth vermilion-red open circular arc flowing into one gently horizontal water ripple, simple bold continuous geometric form, calm and balanced, rounded stroke ends, uniform thick weight, immediately legible at 16px. Flat solid #FF4B4B red, absolutely no gradient, no shadow, no glow, no texture, no 3D, no letters, no Japanese characters, no wordmark, no mockup, no border, no background shape. Transparent background with true alpha. One symbol only, centered square composition, mark filling approximately 75 percent of the square with generous clear padding. Crisp vector-like edges, polished Japanese minimalism. This is the actual application icon asset, not a presentation sheet.

## Upgrade compatibility

- New releases launch `Nagi.exe`, install under `%LOCALAPPDATA%\Programs\Nagi`, and create **Nagi** in Start. Packaging uses `Nagi-win-x64.zip` and `Install-Nagi.ps1` / `Uninstall-Nagi.ps1`.
- Preferences, rules, scripts, index and credentials keep the existing `%LOCALAPPDATA%\Pulse` data location. A rename does not reset user data.
- The Lua context is `nagi.theme` / `nagi.profile`. The earlier `pulse` context remains an alias for existing scripts.
- Source project names, C# namespaces, JSON type names and the single-instance/activation identifiers retain their legacy identities. Older `Run-Pulse.ps1` and `Publish-Pulse.ps1` developer entry points forward to the Nagi scripts.
- The installer closes a running previous installation and migrates a Start menu/startup entry only when it targets the known previous executable. It leaves the previous program directory available for rollback; its old uninstaller can remove it while preserving shared user data.

See the [app guide](USER-GUIDE.md) for controls and [build guide](BUILD.md) for release commands.
