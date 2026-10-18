# Welcome, layout, weather and speakers

Nagi’s early 0.1 series includes a native setup executable and welcome setup. A hand-drawn Nagi / ナギ greeting leads to a language selector before the four setup steps. System default follows your Windows display language; you can switch immediately before continuing. See [languages and translation contributions](LANGUAGES.md). Run **Nagi-Setup.exe**, then open Nagi from Start. Setup installs for the current Windows account without administrator rights. The app and setup are unsigned. The portable ZIP remains available.

1. Enter a local display name. No account is created.
2. Choose Akeno, Dark, Light or System. Choose Vertical with left/right edge, or Horizontal with top/bottom edge.
3. Enable Tools → WebSocket Server Settings in OBS and enter its local port/password. Connect checks status and enables Continue after success. Alternatively choose Skip OBS. Setup never starts recording. A newly entered password is protected for your Windows account when you finish.
4. Search for a weather city and select a result, then Start Nagi. Or choose Skip weather. Celsius and Fahrenheit are supported. No device location permission is requested.

The water drop touches the selected edge before hovering. Hover reveals controls, click pins them, and the next click expands details inward. Horizontal docking follows the work area so the taskbar remains accessible. Layout, edge, name and city can be changed in Settings. Closing welcome saves no draft; the next launch starts setup again. Finish saves your choices, then a dimmed introduction points out the dock. Hover the uncovered drop or choose Okay to dismiss it. Once completed, ordinary launches go straight to the dock. Installer Finish launches Nagi normally; welcome appears only when setup has not been completed. To revisit it later, choose **Settings → General → Run welcome setup**, or run `Nagi.exe --setup`. Existing choices are prefilled; closing a replay keeps the saved configuration.

## Teardrop, dock and app folder

Nagi's dock rests as a small teardrop. Hover the teardrop to reveal the dock; moving away retracts an unpinned dock. The same interaction works at the top, bottom, left or right edge.

<p><strong>Resting teardrop</strong><br><img src="images/nagi-teardrop.png" width="165" alt="Nagi resting as a small teardrop"></p>
<p><strong>Revealed dock</strong><br><img src="images/nagi-dock.png" width="760" alt="The Nagi dock revealed by hovering its teardrop"></p>

Profiles with up to two apps show their shortcuts directly. More apps group all shortcuts into one folder tile. Hover or click the tile to expand those apps; move into the expanded folder and click the app you want. The dock stays in place while the folder opens. No app opens just from hovering. Click the logo for the full control center, where Overview lists all apps in rows of up to four.

<p><img src="images/nagi-app-folder.png" width="760" alt="Hovering the app folder reveals extra apps above the dock"></p>

## Weather

Search text goes to Open-Meteo only when Search cities is pressed. Enabled weather sends the selected coordinates and units. Nagi makes no GPS or IP lookup. Conditions cache for 15 minutes; failures retry after two minutes and retain a clearly labelled last reading where available. Disabled weather makes no forecast requests. These are model-based current conditions, not a local sensor reading. Attribution appears on the weather card.

The service restricts requests to fixed provider hosts, limits responses to 256 KB, applies an eight-second deadline and discards results for superseded settings. See [Open-Meteo weather](https://open-meteo.com/en/docs) and [geocoding](https://open-meteo.com/en/docs/geocoding-api).

## Music and speakers

Artwork appears when supplied by a Windows media session. The visualizer shows recent measured **system output peaks**, including other active apps. It is not a frequency spectrum. Silence produces a quiet line; reduced motion displays the current level without transitions. No raw audio is captured, saved or transmitted.

Choose **Speakers** in Overview or the media preview, select connected headphones/speakers, then press **Use speaker**. This changes Windows console/media defaults and leaves communications routing alone. Apps with their own routing may retain that choice. Windows sound settings is available in the dialog.

Enumeration uses public Core Audio interfaces; default switching uses Windows PolicyConfig COM, which is not a public SDK contract. Errors remain visible and do not disable the Windows settings fallback. The audio sampler detects output changes within 500 ms.

## Installation and removal

Setup installs in `%LOCALAPPDATA%\Programs\Nagi`, creates a Start menu shortcut and registers an Apps & features uninstaller. It does not enable startup unless you previously opted in. Existing settings, scripts and credentials remain in `%LOCALAPPDATA%\Pulse` for compatibility.

Remove through Windows Settings → Apps → Installed apps → Nagi, or run Uninstall-Nagi.ps1. Native removal deletes only the generated list of published files; unknown files and user data are preserved. Close-Nagi.ps1 gracefully closes only the matching installed application, including its welcome window.

## Implementation and checks

WelcomeWindow persists the normalized draft only on Finish. Future read-only settings schemas bypass setup. DockLayout reuses the same controls for both orientations; HaloWindowManager clamps them to monitor work area and DPI. EdgeDropContour supplies a shared liquid silhouette: Win2D draws the surface while a matching native region clips the complete window during the spring transition. Expanded controls retain DWM rounding.

Use [build instructions](BUILD.md) for NSIS setup creation and the welcome smoke helper. Use `--data-dir` with an absolute scratch directory for automated UI checks; the helper deliberately uses a test name and city. Each scratch profile has separate settings, logs and activation events. See [validation](VALIDATION.md) for verified results and remaining limits.
