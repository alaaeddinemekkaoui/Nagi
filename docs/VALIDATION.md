# Nagi v0.1.1 validation

## Automated checks

The Windows Release build passes. The automated suite contains **203 core/infrastructure tests and 53 Windows integration tests (256 total)**. It covers settings recovery, profile rules, workspaces, search, media policies, motion, power status, providers, native geometry, OBS protocol handling and verified update preparation.

OBS tests use an owned loopback fixture and confirm observed recording, streaming and program-scene states. They do not start the user's recording or stream. Update tests cover stable versions, download URLs, SHA256 and length verification, archive completeness and path rejection. NuGet vulnerability-data retrieval was unavailable during the recorded build (NU1900).

## Isolated desktop checks

- `Smoke-NagiThemes.ps1`: the old build crashes on its first Akeno → Light change with `E_INVALIDARG` (`0x80070057`) in the base acrylic configuration-change handler. The fixed build exercises repeated Akeno/Light/Dark/System changes, saved choices, glass on/off and collapse/reveal. An owned solid-color window behind the actual desktop capture checks pixels outside the native teardrop region; forced DWM rounding reproduced the unwanted pill, and disabling it removes those pixels.
- `Smoke-NagiWorkspaces.ps1`: control center, app add/remove, optional widgets, settings navigation, Akeno choices, music preference, Default/Stream profiles, four-app rows and OBS hover controls.
- `Smoke-NagiAppFolder.ps1`: Horizontal and Vertical teardrop reveal, three direct shortcuts, app-folder hover/click expansion, pointer movement, stable dock position, 16-app profiles, empty Default profile and removal back to three apps.
- `Smoke-NagiUpdate.ps1`: staged replacement, preference retention, restart/data-directory arguments, result reporting, locked-file rollback and path rejection using a test executable.

These scripts use isolated settings. Hover checks do not launch apps. Screenshots are in `docs/images`; detailed local evidence is in the Git-ignored `artifacts/validation` directory.

## Distribution and broader coverage

The release includes a self-contained ZIP, per-user installer and standalone portable EXE. The portable EXE prepares its bundled files in the user's local application-data folder and starts Nagi without installation. Portable-launcher validation is recorded with the release build.

Physical monitor hotplug/mixed DPI, exclusive fullscreen games, graphics-driver differences, real sign-in startup, clean Windows installations, trusted signing, a full Narrator/keyboard matrix and transport across multiple media players require broader coverage. No code-signing certificate is available in this workspace. Live release-to-release updates and real broadcasting are separate from the controlled fixtures.
