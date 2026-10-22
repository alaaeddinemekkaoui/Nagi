# Changelog

## Nagi v0.2 — 2026-10-22

- Add a floating Overview / Profiles / Utilities / Settings navigation bar. Utilities brings the existing countdown and city weather together, with a native vector timer ring, start/pause/resume/reset and a per-profile dock-widget switch.
- Refine the single-row dock with a compact stacked timer, rounded music card, artwork and measured audio bars. Keep the resting activity notch sized to its content, with smooth theme-matched edges and completion check beside 00:00.
- Expand the offline JSON collection to 40 language packs, including Japanese, Simplified Chinese and Traditional Chinese; recognize Chinese system-language script parents and keep contributor language discovery automatic.
- Launch the Nagi showcase website with interactive feature and theme previews, a roadmap, Windows download and live GitHub release version/changelog updates. Refresh the README with current native screenshots and the Akeno samurai artwork.

- Smooth notch edges with matching display-scale geometry and a single antialiased material composite over the host backdrop. Keep glass and solid notches tied to the app theme without a separate rim. Remove tinted artwork backing tiles and duplicate fallback icons; preserve rounded, borderless cover images.
- Render audio bars as rounded vector geometry with smooth size animations. Follow the Windows accent color by default, or choose a custom visualizer color in Music or Appearance settings. Update the notch and media panels immediately, save the choice and translate the controls into all six languages.
- Add a separate Development executable with an English developer menu, direct page navigation, four-edge notch/timer/music/notification previews, hover panels, diagnostic exports and automated UI checks with JSON reports. Production excludes developer tools. Build matching Development/Production portables from the same source and version; development preferences, instance identity and logs are separate and production updates are disabled in Development.
- Stack resting timer, completion and music content vertically in a compact notch on the left or right edge. Keep top/bottom content horizontal and resize the shared silhouette to fit upright content.
- Add a timer hover widget with native icon controls for start/restart, pause/resume and cancel. Profiles with Timer enabled show the saved duration before starting; the panel shares the dock's theme and preserves its position without waking audio meters.
- Separate the dock timer from media and volume into its own aligned button with matching styling. Timer-only profiles no longer show an unwanted volume widget; clicking the countdown opens its controls.
- Move timer duration and automatic activation into each profile, including welcome setup. Preserve older timer preferences, allow independent profile durations, and apply manual profile activation immediately without interrupting an active countdown.
- Fit the resting activity notch to the timer or media content instead of a fixed wide panel. Use compact curved shoulders, a 34-DIP depth, and stable countdown digits; expand only when longer timer text needs room.
- Use a wide, screen-connected notch with a flat body and rounded corners for active timers and music. Keep artwork and visualization side by side on every dock orientation; smoothly return to the small logo teardrop when idle. Share the animated contour across acrylic, painting and native hit testing.
- Keep live countdown and music activity enabled by default in the resting notch. Timer completion shows a green check beside `00:00`; playing music can appear even when the active profile's Media widget is hidden. Give live content enough room inside the curved silhouette, including profiles with a short rail, so digits and artwork do not clip.
- Adapt transition ticks to the current monitor with an optional 60 Hz Efficiency mode. Update the file index incrementally with bounded, debounced filesystem events instead of rescanning unchanged folders every ten minutes.
- Pause hidden GPU, microphone, temperature and FPS sampling. Read output peaks only for visible meters; resting audio refreshes slowly and wakes immediately on reveal. OBS connections and foreground-profile detection remain independent.
- Add configurable countdowns, pause/resume/cancel, a default 30-minute Focus timer, a dock countdown beside volume, optional resting timer/music activity, and a brief green completion check even when the live timer is disabled.
- Add custom-named profiles with create/rename/delete, direct activation buttons and per-profile widgets/apps/rules. Welcome now includes a profile customization step before finishing. Translate the new controls across all six language packs.
- Improve dock readability with slightly larger text, artwork and app icons, higher-resolution native icon sources, and a modestly larger teardrop and vector logo.
- Redesign Profiles as individual cards with dedicated widget, app and executable-activation settings. Add executable browsing, keep rules inside their profile, and refresh the Overview/Profiles/Settings navigation cards.
- Make the notch inherit the window's theme and material. Glass hides separate color controls; Dark and Light offer one shared Windows-accent or custom background tint across the window, dock and notch.
- Make glass backgrounds consistently translucent in the dock, hover panels, Settings, quick search, welcome and local-player windows. Reduce acrylic tint and stacked card/background overlays while preserving readable controls and solid accessibility fallbacks.
- Use a tiny edge-connected teardrop with smooth mirrored Bézier curves and a centered red logo. Remove the shoulder strip, glass rim and notch-button focus rectangle; share the silhouette with window clipping and hover detection on every docking edge.
- Keep horizontal drops and hover reveals above the Windows taskbar instead of covering its icons.
- Render the Nagi logo as DPI-independent vector curves and preserve fractional antialiasing around the glass notch for clearer small-size rendering.
- Smooth native app icon edges with correct transparency and higher-resolution, display-scale-aware images. Round media artwork through the image surface so rectangular corners cannot protrude.
- Keep the drop and dock glass consistent through hover transitions, with Windows accent or custom color tinting.
- Fit hover panels to their content without scrolling. Keep playback, volume, microphone and all app shortcuts visible; scale down together only when screen space is limited.
- Center app icons and labels as a group, align dock icons, fix the folder badge and use an app-window fallback when a native icon is unavailable.
- Remove the duplicate preview audio meter and hide the waveform while media is paused.

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
