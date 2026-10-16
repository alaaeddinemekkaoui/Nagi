# Nagi 1.9.0

Nagi now includes a native installer and a welcome flow: choose your local name, theme, vertical/horizontal layout and edge, optional OBS connection, and optional weather city.

Download the [installer](https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v1.9.0/Nagi-Setup-1.9.0.exe) from the [1.9.0 release](https://github.com/alaaeddinemekkaoui/Nagi/releases/tag/v1.9.0). Run **Nagi-Setup-1.9.0.exe**, then open Nagi from Start. Windows 11 x64 22H2+ is required. Setup installs for your Windows account without administrator rights. The app and installer are unsigned. Verify the adjacent SHA256 file using Get-FileHash.

Nagi starts as a water drop touching the selected edge. Hover reveals the rail, click pins it, and the next click expands details inward. Vertical attaches left/right; horizontal attaches top/bottom. Settings can change these choices later.

Music includes supplied artwork and a measured live output-level visualizer. Speakers selects a connected Windows media output. Optional weather sends explicit city searches and selected coordinates to Open-Meteo; no device geolocation is used. OBS connects only to this computer and never starts recording during setup. Both can be skipped.

Alt+Space opens quick search. Ctrl+Alt+Space opens the control center. Ctrl+Alt+H hides/shows the dock. Quit is available in the system tray.

The portable Nagi-win-x64.zip contains the same self-contained app. Extract all files together and run Nagi.exe. Install-Nagi.ps1 remains available. Remove through Windows Installed apps or Uninstall-Nagi.ps1; preferences and Lua scripts are retained.

See [the complete guide](docs/USER-GUIDE.md), [welcome/layout details](docs/WELCOME-AND-LAYOUT.md), [build instructions](docs/BUILD.md), [validation](docs/VALIDATION.md), and [changelog](CHANGELOG.md). GPU/temperature/FPS depend on available providers and permissions. Tests on this desktop do not certify every monitor, media player or clean Windows installation.

The existing GitHub v1 release remains on Nagi 1.8.0. Its original notes are preserved in [docs/RELEASE-v1.md](docs/RELEASE-v1.md).
