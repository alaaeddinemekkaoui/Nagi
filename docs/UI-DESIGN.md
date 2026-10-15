# Halo 1.1 — glass and motion

The update keeps Pulse a native Windows utility, with a more compact overview and stronger separation between controls, context and information.

## References

- [Raycast for Windows](https://www.raycast.com/windows): inspiration for a compact utility surface, restrained typography, keyboard discoverability and clear grouping. This is a design interpretation of its public product presentation.
- [EarTrumpet](https://eartrumpet.app/): inspiration for a tray-first utility with audio controls close to the desktop.
- [Twinkle Tray](https://twinkletray.com/): inspiration for quick adjustments and a predictable notification-area entry point.
- [Microsoft system backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops): the actual Windows acrylic implementation and policy behavior.
- Apple design principles, adapted through the local apple-design skill: [motion](https://developer.apple.com/design/human-interface-guidelines/motion), [materials](https://developer.apple.com/design/human-interface-guidelines/materials), [accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility). Applied principles are brief/cancellable motion, readable layers, native fonts and optional effects.

No assets or layouts are copied from those products. Pulse keeps its waveform identity and restrained crimson accent.

## What changed

- Thin native desktop acrylic, cooler tint, a subtle header wash, gradient rim and translucent inset cards. There is an opaque appearance switch and high-contrast resource palette.
- Consistent 18-pixel card radii, larger readable secondary text, segmented tabs and an active-context strip shared across pages.
- Inline media transport, a prominent audio slider, shorter metric cards and consistent shortcut buttons. The overview fits into a smaller floating panel.
- Persistent tray icon with native Show/Hide/Settings/Profile/Quit commands and recovery after Explorer restart.
- Ctrl+Alt+Space expands/collapses; Ctrl+Alt+H hides/shows. Both are independently configurable and report conflicts.
- Velocity-preserving critically damped springs for native bounds, compositor-based content fades/scales, staggered entrance and subtle spring feedback for hover/press. No interaction waits for a transition to finish.

## System preferences

The material controller follows Windows policy. Energy Saver, disabled transparency, remote sessions or high contrast can make native acrylic fall back to an opaque color. This is surfaced as the real material state in Settings diagnostics. Pulse does not change Windows power/transparency settings. Both Windows animation preferences and Pulse's Reduce motion option skip transitions.

The implementation supplies smoother motion, rather than claiming a guaranteed frame rate or identical behavior to a different operating system. Mixed-DPI, reduced-transparency and accessibility coverage remains part of the release hardening matrix.

## Halo 1.3 — connected water drop

At rest, a 28 × 84 DIP water-drop silhouette touches the selected display edge. There is no XAML outline or native dialog frame. Hover unfolds a 326 × 84 DIP glance surface without activation. The first click pins it and displays a small pin mark; the next expands the overview. Pointer departure retracts an unpinned glance, while Escape always returns to the edge. CPU/media previews remain available after moving inward from the edge lip.

The native free-edge contour interpolates between a tapered drop and a rounded panel using the same critically damped spring as window bounds. The screen-facing edge stays flat. Left/right placement uses monitor bounds horizontally and the work area vertically; additional previews preserve the compact strip above/below according to available space. Full panels remain attached and clamped to the usable display. Reduced-motion settings snap the contour and bounds together.

## Halo 1.4 compact status and Lua

The compact row contains only the waveform logo, CPU, media/memory and a real Windows battery percentage. The two-line grip and compact wordmark are removed. Battery fill reflects charge; hover describes charging or power source. No-battery systems show AC; missing values are explicit. The logo supports click/pin/expand and a six-DIP drag threshold. Native cursor/mouse-button sampling drives an active drag at 90 Hz, then snaps and saves the closest edge. Lua UI Studio customizes colors, card corners, spacing, labels and overview sections while retaining native docking and controls; see LUA-UI.md.

## Halo 1.5 — Akeno and live media

Akeno uses ink-black acrylic, vermilion controls, a small 朱乃 seal, Japanese subtitle and quiet vector motifs. Windows High Contrast removes the decoration and keeps system colors; reduced motion skips transitions. Lua accents and surfaces remain optional overrides.

Actual player artwork is shared across the 24-DIP compact thumbnail, 88-DIP hover image and 80-DIP overview image. Missing artwork uses the music glyph. Hover output volume and mute work even without a media session. The thin meter follows measured Core Audio peaks with time-based attack/release; it does not invent frequency bars. Audio sampling is independent of slower system/media queries.

Preview height uses the available monitor work area and current DPI, retaining the compact strip location. Native bounds and contour springs are interruptible; contour settling uses a tighter tolerance, and unchanged native regions are reused. The timer requests 120 Hz during transitions; achieved frame rate depends on Windows and the display.

Meter semantics follow [Microsoft IAudioMeterInformation](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudiometerinformation): signal peak precedes endpoint volume attenuation; software metering can report zero for exclusive-mode streams.

## Halo 1.6 — quick search

A centered borderless launcher uses the same theme and Lua brushes, native acrylic, readable two-line results and an explicit keyboard hint bar. Red focus and selection indicators follow Akeno; system contrast colors remain authoritative. Input stays focused while Up/Down select results; Enter executes a selected result, Ctrl+Enter reveals its folder, Escape/outside focus dismisses. A native focus check avoids treating delayed first-window deactivation as an outside click. A 140 ms compositor fade respects reduced motion. Layout clamps to the foreground monitor work area at its DPI.

## Halo 1.7 — vertical rail and clean surface

The screen-attached tab reveals an 80×420 DIP rail with stacked identity/search/CPU/GPU/media/microphone/battery controls. A 350-DIP preview opens inward; expanded panels retain the rail. A scrolling viewport keeps rail controls reachable on short work areas and at larger text sizes. Win2D supplies a Direct2D antialiased rounded fill, while DWM owns native corners. Native polygon regions and decorative card/window outlines are removed; focus indicators remain available. GPU/sensor/FPS/OBS status is genuine or explicitly inactive, with recording commands confirmed against observed OBS output state.

## Nagi 1.8 identity

The product now has one name, Nagi (凪). A generated transparent red arc/ripple mark replaces the waveform in the rail, header, executable and tray. All themes retain the same product mark. Prior sections describe the earlier Pulse / Halo design history; current assets and generation notes are in BRANDING.md.
