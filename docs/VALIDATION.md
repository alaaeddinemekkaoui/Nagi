# Nagi validation

## Automated checks

The Windows Release build passes. The automated suite contains **234 core/infrastructure tests and 56 Windows integration tests (290 total)**. It covers settings recovery, profile rules, workspaces, search, media policies, motion, power status, providers, native geometry, OBS protocol handling and verified update preparation.

OBS tests use an owned loopback fixture and confirm observed recording, streaming and program-scene states. They do not start the user's recording or stream. Update tests cover stable versions, download URLs, SHA256 and length verification, archive completeness and path rejection. NuGet vulnerability-data retrieval was unavailable during the recorded build (NU1900).

## Isolated desktop checks

- `Smoke-NagiLiveUpdate.ps1`: installs a real GitHub release into an isolated copy, checks automatic detection, release notes, Later without exiting, verified download, explicit Update now, process restart, installed version and preserved preferences. `-LegacyClient` covers clients from before the update dialog.
- `Smoke-NagiThemes.ps1` also captures the expanded light surface over an owned black background to verify readable light material, in addition to theme switches, quick-search reopening and teardrop clipping.

- `Smoke-NagiThemes.ps1`: the old build crashes on its first Akeno → Light change with `E_INVALIDARG` (`0x80070057`) in the base acrylic configuration-change handler. The fixed build exercises repeated Akeno/Light/Dark/System changes, saved choices, glass on/off and collapse/reveal. An owned solid-color window behind the actual desktop capture checks pixels outside the native teardrop region; forced DWM rounding reproduced the unwanted pill, and disabling it removes those pixels.
- `Smoke-NagiWorkspaces.ps1`: control center, app add/remove, optional widgets, settings navigation, Akeno choices, music preference, Default/Stream profiles, four-app rows and OBS hover controls.
- `Smoke-NagiAppFolder.ps1`: Horizontal and Vertical teardrop reveal, up to two direct shortcuts, app-folder hover/click expansion, pointer movement, stable dock position, 16-app profiles, empty Default profile and removal back to two apps.
- `Smoke-NagiUpdate.ps1`: staged replacement, preference retention, restart/data-directory arguments, result reporting, locked-file rollback and path rejection using a test executable.

These scripts use isolated settings. Hover checks do not launch apps. Screenshots are in `docs/images`; detailed local evidence is in the Git-ignored `artifacts/validation` directory.

## Distribution and broader coverage

The release includes a self-contained ZIP, per-user installer and standalone portable EXE. The portable EXE prepares its bundled files in the user's local application-data folder and starts Nagi without installation. Portable-launcher validation is recorded with the release build.

Physical monitor hotplug/mixed DPI, exclusive fullscreen games, graphics-driver differences, real sign-in startup, clean Windows installations, trusted signing, a full Narrator/keyboard matrix and transport across multiple media players require broader coverage. No code-signing certificate is available in this workspace. Live release-to-release updates and real broadcasting are separate from the controlled fixtures.


## Language files and welcome selection (2026-10-17)

- 275 automated tests passed: 221 core/infrastructure tests and 54 Windows tests. Language tests load all six real JSON files, check the shared 603 keys, formatted arguments, malformed-file isolation, region fallback, Arabic direction, persisted settings and editable English with embedded fallback.
- `Smoke-NagiLanguages.ps1` opened English, French, Spanish, German, Russian and Arabic welcome windows, verified their translated controls and current-language status, and switched each to English before entering a name. French greeting and Arabic right-to-left screenshots were inspected.
- `Smoke-NagiLanguageRestart.ps1` completed welcome and its dock introduction, selected French in General settings, restarted into a new process, verified translated Settings, and confirmed the saved name, theme and preferred music service remained intact.
- Test instances use separate data directories under `artifacts/validation` and close only their own processes. These isolated checks preceded the v0.1.3 release.

## Dock polish and local playback (included in v0.1.3)

- 289 automated tests pass. Added cases cover the two-app folder threshold, physical-battery visibility, compact dimensions, grapheme-safe title shortening, notch-color normalization and native resize retention.
- `Smoke-NagiThemes.ps1 -Animated` completes 18 Akeno/Light/Dark/System transitions. Light over a black test background averages 243.7/255; the resting contour has zero painted pixels outside its shape across 290 samples, including cold start and glass off.
- `Smoke-NagiPolish.ps1` checks one-row fit without scrolling, widget bounds, app grouping, saved System/Custom notch modes, Settings resize across navigation, local WAV and AVI playback, dock pause/resume, shortened long titles and clean shutdown with the local player open. Final evidence: `artifacts/validation/polish-20261017-232216`.
- Horizontal and Vertical app-folder scripts pass hover/click expansion, stable widget position, pointer movement into apps, 16-app profiles and removal to two direct shortcuts. Horizontal documentation images were refreshed.
- Six real welcome windows pass immediate language switching. The English capital N and subsequent Japanese lettering were visually inspected. All six language packs contain the same 618 entries, including the About credit.
- Hidden audio meters no longer animate, visualizer animations are reused, and unchanged brush/surface writes are skipped. No whole-app CPU or memory benchmark is claimed.
- Playback depends on installed Windows codecs. Physical battery absence is covered by automated cases; this test computer reports a battery. All GUI checks use isolated data directories; the user's installed app remains running independently.
## Language settings and UI consistency review

- Language now has its own entry on the main Settings menu, with current-language status, System default and explicit restart; the General shortcut remains available.
- 290 automated tests pass (234 core/infrastructure, 56 native Windows). A new native test verifies that an interrupted animation retains the actual corner radius.
- `Smoke-NagiUiConsistency.ps1` passes 112 page/size/language combinations across English, French, Russian and Arabic, with Akeno, Light and Dark appearances. It checks 12 settings categories plus Overview/Profiles at 640×460 and 960×720 logical pixels, horizontal overflow, header stability during navigation, the visible language tile and hover bounds. Local screenshots/evidence: `artifacts/validation/ui-review-20261017-235507`.
- `Smoke-NagiLanguageRestart.ps1` changes French from the dedicated Language page, performs a real process restart and verifies translated Settings plus preserved name, theme and music preference.
- Animated theme checks pass 18 transitions with no fatal errors, readable light material and zero painted pixels outside the resting notch across 290 samples. Horizontal/Vertical folder hover and pointer transfer checks pass.
- Navigation fades only the changing content. Hover does not enlarge tiles; press feedback stays within one pixel of each layout edge. Preview headings/actions use separate columns, Arabic navigation mirrors, and the global Settings page hides the workspace banner.
- Dock widget heights and padding follow the measured rail dimensions, including full CPU/GPU percentages, vertical volume/battery and the narrow artwork/volume layout. Both dock orientations are checked for fit without scrolling. No claim of exhaustive visual coverage across every Windows scaling or monitor combination is made.
## v0.1.3 release verification

The About page thanks Reddit user u/alkidnn for suggestions, with a profile link and translated acknowledgment in all six language packs. Release checks include the 290-test suite, four-language Settings/layout checks with visible About credits, a real French-language restart, standalone portable launch and package SHA256 verification.
