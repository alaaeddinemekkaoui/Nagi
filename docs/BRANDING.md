# Nagi identity

**Nagi** (凪, “calm”) is the product name. The current release is **v0.1.1**, published with the `v0.1.1` Git tag. Akeno is an appearance theme.

<img src="images/nagi-logo.png" width="160" alt="Nagi red arc and water-ripple logo">

## Logo and appearance

The mark combines a red open arc and a quiet water ripple. It has no lettering or background tile, so the same shape works in the dock, header, executable and Windows tray. Keep clear space around it and retain its aspect ratio.

| Asset | Source location |
| --- | --- |
| Transparent logo | `src/Pulse.App/Assets/Nagi.Logo.png` |
| Application icon | `src/Pulse.App/Assets/Nagi.ico` |
| Tray icon | `src/Pulse.App/Assets/Nagi.Tray.ico` |
| Akeno Samurai background | `src/Pulse.App/Assets/Akeno.Samurai.png` |
| Documentation logo | [images/nagi-logo.png](images/nagi-logo.png) |

The Akeno background is bundled locally. Choose Samurai, Gradient or None in Appearance & dock. Lua accent colors customize surrounding controls while retaining the product mark.

## App files

Open `Nagi-Portable.exe` directly, extract `Nagi-win-x64.zip`, or install `Nagi-Setup.exe`. The installed app is `%LOCALAPPDATA%/Programs/Nagi/Nagi.exe`. Preferences, profiles, scripts, index and protected credentials are stored in `%LOCALAPPDATA%/Pulse`; updates preserve them. The Lua context is `nagi.theme` / `nagi.profile`.

See the [app guide](USER-GUIDE.md) and [build guide](BUILD.md).
