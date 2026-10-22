# Nagi documentation

Documentation for Nagi v0.2, the Windows 11 desktop companion. Start with [welcome, layouts, weather and speakers](WELCOME-AND-LAYOUT.md) for the setup flow. The floating navigation now includes **Utilities** for the circular timer and weather. [Interactive website](https://nagi-notch.vercel.app) · [Roadmap](https://nagi-notch.vercel.app/roadmap).

Its dock rests as a small teardrop; hover it to reveal the dock. Profiles with more than two apps group their shortcuts into one folder that expands on hover.

Start with the [complete app guide](USER-GUIDE.md). For a first customization, copy the [commented Lua example](examples/simple-ui.lua) into **Settings → Lua UI studio → Edit Lua UI**, then select **Apply script**.

| Document | Contents |
| --- | --- |
| [Branding and logo](BRANDING.md) | Name, logo assets and local file locations |
| [Dock, teardrop and layouts](WELCOME-AND-LAYOUT.md#teardrop-dock-and-app-folder) | Screenshots of the resting teardrop, revealed dock and app folder, with hover/click behavior |
| [App guide](USER-GUIDE.md) | Installation, daily use, controls, profiles, settings, data, troubleshooting and removal |
| [Languages and translations](LANGUAGES.md) | System language, welcome selection, Arabic layout and adding a JSON translation |
| [Quick search](QUICK-SEARCH.md) | Query syntax, shortcuts, app catalog, indexing and limits |
| [Lua UI reference](LUA-UI.md) | Every appearance option, theme/profile context, reload and error recovery |
| [Commented Lua example](examples/simple-ui.lua) | Small, ready-to-paste red-accent customization |
| [Hardware and studio](INTEGRATIONS.md) | GPU, temperature providers, FPS helper, microphone and OBS setup |
| [Build and distribution](BUILD.md) | Development setup, release packaging, installation and signing |
| [Developer tools](DEVELOPMENT.md) | Separate development executable, debug menu, notch fixtures, direct pages and automated UI reports |
| [Architecture](ARCHITECTURE.md) | Projects, data flow, lifecycle, native APIs and persistence |
| [UI design](UI-DESIGN.md) | Visual references, materials, motion and vertical layout |
| [Settings example](settings.example.json) | Example JSON configuration; use the app settings for routine changes |
| [Validation](VALIDATION.md) | Tests, observed runtime checks and remaining environment coverage |
| [Roadmap](ROADMAP.md) | Current limitations, external requirements and future scope |
| [Changelog](../CHANGELOG.md) | Release history |
| [Third-party notices](../THIRD-PARTY-NOTICES.md) | Libraries and bundled-tool licensing |

These documents and the Lua example ship in the portable ZIP and per-user installation. Developer commands assume a source checkout; end users can run the extracted release without installing a .NET SDK.
