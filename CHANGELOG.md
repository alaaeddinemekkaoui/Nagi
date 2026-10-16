# Changelog

## Nagi version 1 — 2026-10-16

The consolidated version 1 release includes the earlier system, media, search, Lua and integration features plus workspaces, category settings, four-edge layouts, weather, speaker selection and native setup. Git history and earlier development tags remain available.

- Welcome appears only on first launch unless explicitly reopened from Settings; setup upgrades no longer force it.
- Finishing welcome saves the user's choices, then dims the selected monitor with a real input/visual hole around the dock.
- A friendly thank-you points out the selected edge. Hover the drop, choose Okay, or press Escape to dismiss and remove the dimmer.
- Redesigned the README around version 1 with portable ZIP and EXE downloads, screenshots and the complete feature overview.
- Release downloads are `Nagi-win-x64.zip` and `Nagi-Setup.exe`; the application build version is 1.0.0.

## 1.10.0 — 2026-10-16

- Added a category grid for Settings, with individual pages and back navigation.
- Added editable Work/Gaming/Focus app docks and per-profile widget visibility. Work suggests VS Code, Terminal and Browser; Gaming suggests Steam and Browser.
- Added opt-in app opening on explicit profile activation. Automatic detection and startup only change the dock/widgets.
- A single logo click opens a large centered control center with profile management.
- Added microphone status and mute controls immediately below media and in its preview.
- Made Glass following Windows light/dark mode the default for new settings.
- Added a locally bundled generated Akeno samurai background with Samurai, Gradient and None options.
- Clipped the actual native window silhouette to remove opaque corner patches; attached rail edges remain flat and inward corners rounded.

## 1.9.1 — 2026-10-16

- Fixed dark corner patches by painting the full client area inside the native rounded window.
- Removed the clipped Japanese decorative arc from the compact rail.
- Reflowed horizontal media artwork, title, measured visualizer and volume; hidden weather no longer reserves a column.
- Installer Finish opens welcome even after an upgrade. Settings → Run welcome setup revisits all four steps with saved choices prefilled; cancel preserves preferences.
- Added isolated development profiles and independent activation events for UI checks.

## 1.9.0 — 2026-10-16

- Added a native per-user NSIS installer and Apps & features removal, preserving preferences and scripts.
- Added a four-step welcome flow: local display name, theme/layout/edge, optional verified OBS connection, and optional city weather.
- Added horizontal top/bottom docking alongside vertical left/right docking, with inward-opening details and work-area/DPI bounds.
- Reshaped the resting tab into an edge-attached water drop with a Win2D surface and spring-driven contour.
- Added optional Open-Meteo city search, current conditions, Celsius/Fahrenheit, caching and offline states.
- Added a live measured output-level history visualizer and connected speaker/headphone selection.

## 1.8.0 — Nagi

- Renamed the app to Nagi (凪), with one name across the rail, panels, quick search, tray, executable, installer and documentation.
- Added a generated minimalist red arc/ripple logo and transparent app/tray icons at 16–256 pixels.
- Preserved the existing user-data location and activation identity for upgrades. Lua now exposes `nagi` while retaining `pulse` as a compatible alias.
- New releases use `Nagi.exe`, `Nagi-win-x64.zip` and Nagi installation scripts. Existing source project names and developer command aliases remain compatible.

## 1.7.0 — 2026-10-15

- Replaced the horizontal compact strip with a vertical edge rail and inward-opening previews/panels. Short work areas can scroll the rail.
- Replaced polygon window regions with native DWM rounding and a Win2D antialiased surface; removed decorative card/window outlines and residual native edge styles.
- Added actual GPU engine usage, microphone mute, WMI temperature providers, optional PresentMon capture/helper, and authenticated local OBS status/recording controls.
- Protected OBS credentials with per-user DPAPI; verified recording state after commands rather than treating acknowledgement as recording success.
- Added graceful installer shutdown, provider notices, a certificate-based signing command, and broader integration tests.

## 1.6.0 — 2026-10-15

- Added an Alt+Space launcher with keyboard navigation, type filters, accent-insensitive and initials matching, a calculator, and explicit web searches.
- Discover installed desktop and Store apps through Windows; index filenames and folders locally with configurable scope, bounded cache, background refresh, cancellation and rebuild controls.
- Added profile/audio/visibility/settings/date/time actions, open-folder and copy-path controls, a configurable search shortcut, a Halo search button and tray entry.
- The search window follows the selected theme, Lua colors, display DPI and work area; outside click and Escape dismiss it.

## 1.5.0 — 2026-10-15

- Added the Akeno ink-black/vermilion theme, Japanese seal and abstract motifs, with Lua customization and system contrast support.
- Added compact media thumbnails, larger artwork, hover output controls and a real Core Audio signal meter.
- Separated audio into a cached 50 ms sampling loop; synchronized external volume changes and throttled slider writes.
- Retry late media artwork and periodically refresh unchanged tracks.
- Fit previews to available screen space, preserve the compact anchor, and smooth contour settling while avoiding redundant native region updates.

## 1.4.0 — 2026-10-15

- Added native battery percentage and charging details, with explicit unknown/no-battery states.
- Removed the compact drag grip and wordmark; the Pulse logo supports click-to-open and drag-to-move gestures.

- Added a native Lua UI studio with an editor, three presets, immediate Apply, explicit file Reload and an opt-out that restores the built-in design.
- Added theme/profile-aware accents, translucent card fills, corners, spacing, metric labels and overview section visibility.
- Embedded MoonSharp with a restricted declarative API, source/instruction limits, off-thread evaluation, validation and last-working-appearance recovery. Scripts are local and editor saves retain a backup.

## 1.3.0 — 2026-10-15

- Replaced the floating pill at rest with a water-drop tab integrated into the left or right screen edge.
- Added hover-to-reveal minimal controls, first-click pinning, second-click expansion, and automatic retraction for unpinned controls.
- Removed both the XAML outer rim and native dialog-frame chrome; kept the attached side flat and morphed the free edge with the existing spring animation.
- Added left/right placement, vertical positioning and cross-edge drag persistence, with migration of older anchors.
- Retained CPU/media previews, full-panel shortcuts, tray recovery, glass and reduced-motion preferences.

## 1.2.0 — 2026-10-15

- Added compact CPU/memory/network and media hover previews with live data, artwork and capability-aware transport controls.
- Added Show more and close actions, delayed hover opening, pointer-exit dismissal and nonactivating spring transitions that preserve the compact strip's anchor.
- Kept paused media visible in the compact pill and reused the existing media commands and shared snapshots.
- Added four-anchor preview placement tests and a physical-pointer hover smoke helper.

## 1.1.0 — 2026-10-15

- Redesigned Halo with native thin acrylic, translucent layers, gradient rims, dark/light/high-contrast palettes and an opaque appearance option.
- Added native system tray controls for Open, Hide, Settings, profiles and Quit, including Explorer re-registration and icon cleanup.
- Added independently configurable Ctrl+Alt+H hide/show shortcut with conflict recovery.
- Added interruptible native-window springs and GPU composition transitions/hover/press feedback with reduced-motion support.
- Compacted the overview, media transport and audio layout; added a reusable waveform mark.
- Fixed visibility checks to use the actual foreground window while preserving application context.
- Added spring, tray lifecycle and separate-shortcut tests plus live tray smoke tooling.

## 1.0.0 — 2026-10-15

- Created the Pulse solution and native WinUI 3 Halo interface.
- Added real CPU, RAM, network, foreground, Windows media session and Core Audio services.
- Added Work/Gaming/Focus profiles, locking, automatic rules, priorities and debounce.
- Added validated profile-filtered quick actions and editable configuration.
- Added persistent display/anchor/drag positioning, global shortcut, single instance and opt-in Windows startup.
- Added local atomic settings, field recovery, backups, future-schema protection and bounded diagnostics.
- Added automated behavior/Windows integration tests and portable/per-user packaging scripts.
