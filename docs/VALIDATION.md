# Validation record

## Nagi 1.9 setup and layouts — 2026-10-16

- 200 automated cases pass: 159 core/infrastructure and 41 Windows. Added welcome/settings round trips, orientation normalization, bounded weather parsing, cancellation, unit/culture handling, caching/stale/backoff states, nullable skipped-weather reload without false recovery, four drop contours, horizontal native geometry and connected-speaker enumeration/routing. Speaker setter checks write the existing multimedia endpoint and leave other routing roles alone.
- Release compilation succeeds. A later package restore reports NU1900 because api.nuget.org is unreachable for vulnerability data; compilation itself has no errors. The earlier restore/build completed with no warnings. This network result does not establish a fresh vulnerability audit.
- Actual welcome checks pass: required name validation, Light/Akeno selection, horizontal layout/edge selection, authenticated connection to an owned local OBS fixture, unavailable OBS/skip, skip weather, saved greeting/preferences, direct dock startup after completion, and cancel/close without saving a draft. No recording was started during welcome checks. The OBS fixture was closed afterward.
- Live checks pass on Top, Bottom, Left and Right: flush attachment, resting drop, hover controls, pin/expand, inward-opening previews and screen fit. Horizontal media previews expose Speakers without scrolling. Both vertical edges retain physical-pointer focus, original-position clicking, battery percentage, logo drag, CPU preview, Show more and quick-menu checks.
- The Speakers dialog lists real connected outputs. Routing to the existing multimedia endpoint passes the Windows COM test. Choosing a different physical speaker still depends on the devices connected to the user's machine.
- Native setup lifecycle passes: per-user install, version 1.9.0 metadata, Start menu shortcut, HKCU Apps & features registration, installed launch, graceful removal, exact settings-file retention and preservation of an unknown file in the install folder. The native uninstaller is also callable through Uninstall-Nagi.ps1.
- Live geocoding through WeatherService returned six Tokyo results. Separate UI lookup attempts timed out, and the forecast hostname failed DNS resolution from this environment. Timeout/unavailable UI states were observed; successful forecast parsing and stale recovery are covered with controlled HTTP responses. A live forecast reading remains unverified on this network.
- Existing user preferences and OBS credentials are backed up before UI checks and restored afterward; test names/cities are not left as the user's configuration. The final installed copy opens welcome for the user to personalize.
- Broader physical multi-monitor/DPI, player, sign-in, clean-machine, signing and accessibility matrices remain as documented below. Setup remains unsigned.

Implementation environment: Windows 11 x64, OS 10.0.26300, .NET SDK 10.0.113, Windows SDK 10.0.26100, a primary display at 125% scale. Validation was performed on 2026-10-15.

## Automated

- `dotnet build Pulse.sln -c Release`: successful, zero warnings and errors.

## Nagi 1.8 name and logo

- Release build succeeds with zero warnings/errors. All 167 tests pass (131 core/infrastructure and 36 Windows), including the new `nagi`/legacy `pulse` Lua-context compatibility check. Native startup round-trip coverage uses the Nagi registry name.
- The generated transparent logo is used by the rail and header. Windows app/tray ICO files contain eight sizes from 16 to 256 pixels; native tray registration tests accept the icon. Executable metadata reports Nagi, version 1.8.0.
- The packaged `Nagi.exe` passes both-side rail/logo/drag/hover/pin/expand/CPU-preview/menu checks. Nagi title, header mark and Akeno appearance were visually inspected. Alt+Space app/calculator/filename/command navigation and tray Quick search/Open/Hide/Settings/Quit pass under the renamed labels.
- Actual per-user installation passes shortcut migration, installed launch, graceful uninstall and exact preference-file retention. A temporary owned legacy startup entry migrates to Nagi, with the original registry values/types restored after the check. The installer never enables startup on a fresh installation by default.
- A fresh-install regression caught PowerShell throwing when the legacy startup property was absent. Optional registry reads now use `RegistryKey.GetValue` with a null fallback. The absent-entry install/launch/uninstall/preferences check passes after that fix.
- All 58 local documentation links/anchors resolve. User data keeps the existing Pulse directory, activation identity and compatible Lua context so the rename preserves preferences and scripts. Historical 1.7 and earlier checks below retain their original names.

## Documentation and Lua example

- The complete app guide and documentation index cover the 1.7 interface, controls, configuration, integrations, local data, troubleshooting and distribution. Earlier architecture references to the horizontal rail/custom window regions were corrected, and the example JSON includes search, Lua, FPS and OBS settings.
- All 54 local documentation links/anchors resolve, and `settings.example.json` parses successfully.
- `docs/examples/simple-ui.lua` is evaluated by the actual `LuaUiService` in a temporary validation console: all nine Dark/Light/Akeno × Work/Gaming/Focus contexts succeed. This check does not write the user's active script or preferences.

## Halo 1.7 vertical rail / providers

- Final portable 1.7 checks pass on both edges, with actual media artwork, independent audio refresh, synchronized output volume and work-area fit. Alt+Space searches 276 installed apps and 40,000 indexed files/folders (the configured cap), with calculator, command filters, keyboard navigation, Escape/reopen, index rebuild and Settings navigation passing. Lua apply, instruction-budget errors, file reload, appearance retention and opt-out pass.
- Actual tray Quick search, Open, Hide, Settings recovery and Quit pass. This Windows build exposed a native popup window through UI Automation without menu-item children; the smoke helper now falls back to native popup labels/rectangles and still clicks real menu items. Glass material reports Active.
- Actual per-user installation passes: package resources and Start menu shortcut exist, the installed executable launches, uninstall gracefully stops only that installation, removes its directory/shortcut and preserves the exact preferences file. This is a lifecycle check on the current desktop, not a clean-OS or actual sign-in certification.

- Release build passes with zero warnings/errors. 130 core/infrastructure cases and 36 Windows cases pass (166 total, no skips). Added GPU aggregation, temperature/FPS validity, fresh external FPS snapshots, DPAPI credential recovery, OBS v5 authentication/status/recording-state checks, vertical geometry, microphone idempotent mute, and startup registry round-trip with restoration.
- Both-side live rail checks pass: edge attachment, 80×420 DIP vertical controls, 18×132 tab, logo drag, hover/pin/expand, CPU inward preview, Show more and quick menu. Source screenshots show the Win2D/DWM surface without the white outer outline. Some pointer automation attempts were interrupted or missed dwell; the isolated media/audio hover check subsequently passed, including independent audio frames and synchronized actual output volume.
- Real Intel UHD counter values are available (observed about 3–14% during probes). The real default communications microphone reports a bounded volume/mute state; tests write only its existing mute value. No PCM is recorded by Pulse.
- An isolated, checksum-verified OBS Studio 32.2.2 portable fixture uses only an empty scene with no desktop/microphone sources. Authenticated status reports 30 render FPS; local recording start/status/stop pass. A bad-output-path fixture exposed that OBS can acknowledge StartRecord without becoming active; Pulse now verifies the state, with regression tests for accepted-but-inactive output. No streaming command was sent. The fixture was gracefully closed after validation.
- PresentMon 2.6.0 has a valid Intel Authenticode signature and matches the release checksum. A real non-elevated trace probe returned exit 6 / access denied. The product reports permission failure; helper/CSV freshness and target-PID behavior are tested. No live game FPS is claimed on this token.
- No temperature WMI provider and no code-signing certificate exist on this machine. Provider support and the signing command are implemented; physical thermal values and trusted signatures cannot be certified here. No kernel driver or trusted-root certificate was installed.
- Visible keyboard-focusable controls in the inspected live view have accessible names. Native keyboard/shortcut checks and reduced-motion logic remain covered; a full Narrator/high-contrast/mixed-DPI physical matrix remains environment coverage.

## Halo 1.6 quick search

- Release build passes with zero warnings/errors; 124 core/infrastructure cases and 25 Windows integration cases pass (149 total, no skips). New tests cover ranking/filters/accents/initials, bounded arithmetic, explicit web escaping, metadata-only locked-file indexing, cache/scope/cancellation handling, preference persistence, the actual installed-app catalog and third-hotkey conflicts/release.
- `Smoke-PulseSearch.ps1` uses actual Alt+Space and UI Automation. The live source build discovers 276 apps, finds a local filename fixture, evaluates 24 * 7, filters commands, closes with Escape, reopens, and executes the Settings action. It uses a temporary folder scope and does not change playback/volume or launch arbitrary user files.
- Live testing caught an unsupported Applications-folder parsing path; switching to the documented shell:AppsFolder path fixed it. The integration test now requires a populated catalog rather than accepting an unavailable-provider result. Fresh-start automation exposed a delayed WinUI deactivation event. Dismissal now checks native foreground ownership after activation settles; the fresh packaged process passes.
- Chrome and Edge are installed; Spotify is not installed on the implementation machine. Spotify-specific artwork/playback remains a media compatibility check for a machine with Spotify installed.
- Physical mixed-DPI/multi-monitor changes, full Narrator coverage, other languages/keyboard layouts, and folders on removable/unavailable drives remain broader coverage items.

- The final packaged 1.6 build passes fresh Alt+Space opening, app/file/calculator/command searches, Up/Down navigation, Escape/reopen, index rebuild and Settings navigation. The borderless Akeno launcher screenshot was inspected. Both-side edge/battery/logo/drag/hover/pin/expand checks pass, along with live audio frames, synchronized output volume and preview screen bounds. A real media session was available during these regression checks; actual artwork in the hover panel and compact thumbnail was visually inspected. No playback or audio-setting command was sent.

- The portable tray Quick search, Open, Hide, Settings recovery and Quit checks pass. Original preferences were restored, with new search defaults (Alt+Space and standard personal folders) retained. The source/published assemblies match; the portable ZIP includes the quick-search guide.

## Halo 1.5 Akeno / media / audio

- Release build passes with zero warnings/errors. The solution suite passes 97 core/infrastructure/interaction cases and 23 Windows integration cases (120 total, no skips). New coverage checks measured audio envelope behavior, frame-rate independence, preview fit across resolution/DPI, contour settling, cached native audio and disposal.
- The live source application passes both-side edge/battery/logo/drag/hover/pin/expand/CPU/menu checks at 125% DPI. Akeno selection, independent audio frames while Settings is open, matching main/hover volume controls and media-preview work-area bounds pass. Screenshots were visually inspected.
- No active media session was exposed during this pass: fallback UI was checked, but player-supplied artwork and transport require the media-app matrix below. The output endpoint was muted; this pass does not claim an audible playback check.
- Live audio samples volume/mute/peak every 50 ms using cached Core Audio interfaces. The meter reflects signal activity before endpoint attenuation, not a frequency spectrum or perceived loudness. Exclusive-mode software metering can report zero.
- An unpackaged-host HighContrastChanged subscription failed during startup testing; it was replaced by observing the cached system preference during regular UI refresh. The corrected app starts and passes the live checks.

- The portable 1.5 build passes the same live Akeno/audio/preview-fit and both-edge smoke, plus actual tray Open/Hide/Settings/Quit. Source and packaged assemblies match. Lua apply/reload/error/opt-out smoke passes on the source build. Test Lua files were moved out of the user scripts folder and original settings restored with Akeno as the intentional theme change.

## Halo 1.4 Lua UI

- Battery addition: 88 core/infrastructure/interaction/motion cases and 21 Windows integration cases pass (109 total, no skips). Power cases cover empty/full/charging/plugged-in/unknown/no-battery values and Windows' 255/128 sentinels. `Smoke-PulseEdge.ps1` now also compares the displayed percentage with Windows, verifies the grip/wordmark are absent, and drags the logo before expanding it.
- Live checks pass on both screen edges, including battery agreement with Windows, logo-only appearance, drag movement, subsequent click expansion, edge attachment, original drop-position clicks, CPU preview and quick-menu recovery. Dragging uses native mouse-button/cursor data sampled at 90 Hz only during a logo press, because routed pointer events can be missed in the nonactivating WinUI window. The smoke helper lets WinUI dispatch button release before moving the synthetic cursor away.

- 78 core/infrastructure/interaction/motion cases and 20 Windows integration cases pass (98 total, no skips). The new cases exercise presets with theme/profile context, invalid types/options/colors/numbers, missing libraries, infinite loops, bounded file reads, backup persistence and settings opt-in.
- `scripts/Smoke-PulseLua.ps1` exercises the actual WinUI editor: apply a custom label and hide a section, reject an endless script without closing the editor, cancel without changing appearance, reload an edited external file, retain appearance after an invalid reload, and restore built-in labels/sections when disabled. It writes the local UI script, so preserve scripts/preferences before running it. `-Screenshots` saves a cropped custom overview.
- Initial Release build succeeded with no compiler errors; NuGet vulnerability lookup briefly failed (NU1900). A subsequent forced restore succeeded. SDK 10.0.113 was used.
- Final Release build completed with zero warnings and errors. The complete 98-case solution suite passed again after restore. The custom overview was visually inspected at 125% DPI; accent brushes, chart strokes, 24-pixel card corners and section spacing updated live.
- The final portable 1.4 build passes Lua editor/apply/reload/error/opt-out checks, both-side battery/logo/drag/hover/pin/expand checks, and real tray Show/Hide/Settings/Quit. Disabling Lua restores metric labels immediately, independent of sampling cadence. The final core/native suite passes 109 cases. Original preferences were restored after testing; validation scripts and screenshots remain outside the portable ZIP.

## Halo 1.3 edge dock

- 53 core/infrastructure/interaction/motion cases and 20 Windows integration cases pass (73 total, no skips). The Windows cases run sequentially because they share Explorer, native desktop windows, global hotkeys and Core Audio.
- New interaction tests cover hover retraction, first-click pinning, second-click expansion, explicit dismissal and migration from old anchors.
- Native geometry tests cover left/right screen attachment at centered and extreme offsets, stable compact-strip placement during preview, and native frame removal: client dimensions equal window dimensions.
- `Smoke-PulseEdge.ps1` exercises real pointer hover/click interaction and UI Automation on both screen edges. It also verifies CPU details and Show more. It can save app-region screenshots with `-Screenshots`. It changes placement during its run; use a development settings file or save/restore preferences around it.
- `Smoke-PulseHover.ps1` forwards to the edge smoke helper. The broader `Smoke-Pulse.ps1` understands the pin-then-expand sequence. Earlier 1.1/1.2 records below are historical.
- `dotnet test Pulse.sln -c Release --no-build`: 44 core/infrastructure/motion tests and 9 Windows integration tests; all pass, no skipped tests (53 total).

## Halo 1.2 hover previews

- The updated suite passes 44 core/infrastructure/motion tests and 13 Windows integration cases (57 total). Four new anchor cases verify that previews retain the compact strip's screen position and never reveal a hidden native window.
- `scripts/Smoke-PulseHover.ps1` uses physical pointer coordinates and UI Automation to verify CPU hover, moving into preview content, dismissal on exit, media/memory hover, Show more and preservation of foreground focus. The implementation machine exercised an available paused media session; no playback-changing command was sent.
- Hover previews share existing snapshots and media capability flags. Native pointer sampling runs only in compact mode to cover enter/exit notifications missed by the nonactivating WinUI window; opening and closing require dwell/grace delays. Native layout springs and composition transitions retain existing Windows/app reduced-motion behavior.
- Full-panel clicks, navigation, dragging, hiding and shutdown cancel pending previews. Explicit dismissal suppresses reopening until the pointer leaves the compact indicators.
- Build validation for this update uses .NET SDK 10.0.113. The earlier 1.1 records below remain historical.
- Core tests cover manual overrides, priorities/ties, disabled rules, exact identity/path matching, debounce, fullscreen nonclassification, settings round-trip, corrupt/partially invalid/locked/future settings, atomic backups, casing/unknown fields and shortcut validation.
- Windows tests read real CPU/memory, foreground, media and audio; verify Core Audio setters by writing existing values unchanged; verify hotkey collision/release and clamped positioning with a missing display and large offsets. Position tests ensure a hidden window is not accidentally revealed.
- `scripts/Smoke-Pulse.ps1` ran against a live Release application. UI Automation verified expansion/collapse, Focus/Work profile behavior, rule/action editors, settings, hotkey editor, actual Ctrl+Alt+Space handling and second-instance activation.
- The same smoke checks passed against `artifacts/Pulse-win-x64/Pulse.App.exe` after correcting the unpackaged publish resource omission. The ZIP includes the app's PRI, compiled XAML, runtime libraries, icons, installer scripts and documentation.

## Halo 1.1 glass / tray / motion checks

- The redesigned Release application builds without warnings/errors. Native acrylic diagnostics reports `Active` on the implementation machine.
- Spring tests cover 30/60/120/144 frame pacing, convergence without overshoot, continuous velocity when retargeted, equivalent trajectories across frame pacing and instant reset for reduced motion.
- Native tests register, query, recreate and dispose a real tray icon using a unique test GUID. Separate hotkey registration/conflicts are tested independently of the expand/collapse shortcut.
- Actual Ctrl+Alt+H presses hide and restore the native Halo window. The native icon is present in Windows' hidden-icons overflow.
- `Smoke-PulseTray.ps1` uses actual pointer clicks on that icon and menu items. Open, Hide, Settings recovery from hidden state and Quit all passed; Quit terminated the process. The script restores the pointer afterward.
- UI Automation checks cover profiles, editors, settings, hotkey dialog, expansion/collapse, the actual expand/collapse key and second-instance activation. Outside dismissal/fullscreen hiding were temporarily disabled for deterministic automation, then restored to their original preferences.
- Native menu WM_COMMAND handling supports accessibility clients in addition to physical selection. The tray smoke helper targets the English Windows 11 overflow layout; other locales still require a manual accessibility pass.

## Runtime observations

The compact pill and expanded control center were inspected at 125% DPI. Live CPU and RAM percentages, physical capacity, network rates, current process, actual Windows media metadata and default audio endpoint information were present. Unsupported media transport controls were disabled according to the session's capability flags. The UI reused one process/window and remained responsive while sampling.

Settings were saved through the UI and then read on restart. Focus collapsed Halo. Returning to Automatic cleared the manual lock. A second launch exited and revealed the original process. Graceful quit wrote a lifecycle stop entry. Clicking away from the expanded panel collapsed it under the default preference.

Light theme was selected and saved through the UI. After quitting and reopening the published application, its theme selector restored Light. Dark was then restored. The development settings finish in Automatic with no profile lock and dark appearance.

## Still requires broader interactive validation

- Physical multi-monitor hotplug, monitor-to-monitor drag while changing DPI, taskbars on different screen edges, mixed scaling and resolution changes.
- Fullscreen games/exclusive modes and visibility behavior across different graphics drivers.
- Media transport against multiple supported media apps; no playback-changing command was sent to the user's active media during implementation testing.
- Startup at an actual Windows sign-in, per-user installer/uninstaller execution on a clean machine, and signed distribution.
- Narrator, high-contrast themes and the complete keyboard navigation matrix.

These are implemented paths awaiting broader environment coverage, not claimed interactive passes. Optional GPU, temperature, FPS, OBS and microphone controls are deliberately unsupported and labeled unavailable.
