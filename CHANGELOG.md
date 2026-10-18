# Changelog

## Nagi v0.1.3 — 2026-10-18

- Add English, French, Spanish, German, Russian and Arabic language files, system-language detection, welcome language selection, and Settings → Language with a restart control. Contributors can add a JSON file without changing code.
- Draw Nagi, fade it out, then draw ナギ during welcome; respect reduced motion and Windows animation settings.
- Keep the dock compact without scrolling. Show up to two app shortcuts directly; larger profiles use one hover folder. Show full long media titles on hover and retain artwork/volume on narrow displays.
- Add theme, Windows accent and custom notch colors. Connect glass before expansion and remove native frame/shadow flashes.
- Fix repeated panel entrance animations, shifting hover effects, overlapping preview headings, Arabic navigation and widget padding. Preserve motion and corner radius when a transition is interrupted.
- Open Settings/control center as a standard Windows window with a title bar, resize, minimize and maximize controls. Keep it open when focus changes; Close returns to the dock. Retain size across Settings navigation and the existing content animation.
- Stack CPU/GPU values above their labels and weather/battery values above their icons, using consistent spacing.
- Choose an OBS app shortcut, hover controls or Hidden for each profile. Remove duplicate OBS dock apps and add a hide action to the hover panel.
- Show only the current release changelog in update notes; omit the README and older release sections.
- Add local music/video playback, multiple-file queues and dock controls. Improve Windows media-session selection and clean shutdown with the local player open. Supported formats depend on Windows codecs.
- Hide battery/AC controls when no battery is detected; fit vertical media/battery and full CPU/GPU percentages within the dock.
- Skip hidden audio animations, reuse visualizer transitions and avoid unchanged brush/surface redraws.
- Thank Reddit user [u/alkidnn](https://www.reddit.com/user/alkidnn/) in the README and Settings → About & diagnostics for suggestions to improve Nagi.

## Nagi v0.1.2 — 2026-10-17

- Separate activity and error logs, with timestamps, app versions and full exception details. Both are available in About & diagnostics.
- Clear outline icons where available, with native installed-app icons for the dock and app search, plus outline music-service links.
- Improved light-mode contrast and live theme colors. Quick search and its acrylic surface now use the same theme and follow system changes.
- Automatic update detection shows the changelog with Update now and Later. Later keeps Nagi running; the downloaded update remains available in Settings → Updates.
- Failed automatic checks retry after a temporary network failure. Update checks, choices, downloads, verification and restarts are recorded in the logs.

## Nagi v0.1.1 — 2026-10-17

- Fixed crashes when switching Akeno → Light or between day/night themes. The custom acrylic callback no longer calls the failing base configuration handler or reconnects an already connected configuration.
- Fixed the glass pill behind the resting teardrop by disabling forced Windows corner rounding. Acrylic now runs only on the open dock and panels; the teardrop paints its own clipped shape with transparent surrounding layers.
- Empty Default profile with search, CPU/GPU and now playing.
- Work/Gaming presets and Stream profile; microphone, weather and battery are optional.
- OBS hover controls for recording, streaming and three scene shortcuts.
- Single-line app buttons, up to four per row; extra dock apps group into a folder that expands on hover.
- Music fallback links for YouTube Music, Spotify and Apple Music, with a preferred-service setting.
- Automatic update downloads; Settings → Updates → Install and restart applies the downloaded release.
- Fresh portable EXE in the project root, ZIP and setup for v0.1.1. Nagi remains in early v0.1.x releases; v1 waits for explicit approval.

## Nagi v0.1 — 2026-10-17

Nagi is a glass desktop companion for Windows 11. Your apps, music and useful information, tucked into the edge of your screen.

- Screen-edge teardrop that reveals a horizontal or vertical dock on hover, with inward-opening details and a logo-click control center.
- Default, Work, Gaming, Stream and Focus profiles, editable apps, automatic context rules and optional app opening on explicit activation.
- First three app shortcuts visible on the dock; extra apps grouped into a folder that expands on hover or click. Overview shows up to four apps per row.
- Live CPU, memory, network, GPU and battery information, with optional temperature and foreground-FPS providers.
- Windows now-playing artwork and supported playback controls, measured output visualization, volume/mute and speaker selection.
- YouTube Music, Spotify and Apple Music links when nothing is playing, with a preferred-service setting.
- Optional microphone, weather and battery widgets per profile, with city weather and Celsius/Fahrenheit settings.
- Local OBS connection with protected credentials, recording/streaming controls and three configurable scene shortcuts.
- Quick app/file/folder search, calculator, profile commands and configurable global shortcuts.
- System glass, light/dark and Akeno appearances, optional artwork, reduced motion and a native Lua UI editor.
- First-launch welcome, saved preferences, optional Windows startup, tray controls and recoverable local settings.
- Verified in-app update downloads with install/restart and rollback support.
- Self-contained Windows x64 ZIP, per-user setup and a standalone Nagi-Portable.exe. Every publish refreshes the portable EXE in the project root.
