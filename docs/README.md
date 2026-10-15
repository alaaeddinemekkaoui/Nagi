# Nagi documentation

Documentation for Nagi 1.8, the Windows 11 desktop companion.

Start with the [complete app guide](USER-GUIDE.md). For a first customization, copy the [commented Lua example](examples/simple-ui.lua) into **Settings → Lua UI studio → Edit Lua UI**, then select **Apply script**.

| Document | Contents |
| --- | --- |
| [Branding and logo](BRANDING.md) | Name, logo assets, generation prompt and upgrade compatibility |
| [App guide](USER-GUIDE.md) | Installation, daily use, controls, profiles, settings, data, troubleshooting and removal |
| [Quick search](QUICK-SEARCH.md) | Query syntax, shortcuts, app catalog, indexing and limits |
| [Lua UI reference](LUA-UI.md) | Every appearance option, theme/profile context, reload and error recovery |
| [Commented Lua example](examples/simple-ui.lua) | Small, ready-to-paste red-accent customization |
| [Hardware and studio](INTEGRATIONS.md) | GPU, temperature providers, FPS helper, microphone and OBS setup |
| [Build and distribution](BUILD.md) | Development setup, release packaging, installation and signing |
| [Architecture](ARCHITECTURE.md) | Projects, data flow, lifecycle, native APIs and persistence |
| [UI design](UI-DESIGN.md) | Visual references, materials, motion and vertical layout |
| [Settings example](settings.example.json) | Example JSON configuration; use the app settings for routine changes |
| [Validation](VALIDATION.md) | Tests, observed runtime checks and remaining environment coverage |
| [Roadmap](ROADMAP.md) | Current limitations, external requirements and future scope |
| [Changelog](../CHANGELOG.md) | Release history |
| [Feature status](../TASKS.md) | Implemented and verified feature tracking |
| [Third-party notices](../THIRD-PARTY-NOTICES.md) | Libraries and bundled-tool licensing |

These documents and the Lua example ship in the portable ZIP and per-user installation. Developer commands assume a source checkout; end users can run the extracted release without installing a .NET SDK.
