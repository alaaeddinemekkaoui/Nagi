# Nagi roadmap

Nagi is an early Windows desktop companion. The website's `/roadmap` page groups current source features, ongoing validation and future directions. The current code may include features beyond a published package: consult the [latest GitHub release changelog](https://github.com/alaaeddinemekkaoui/Nagi/releases/latest) for the contents of a particular release.

## The foundation

- Adaptive edge drop and live timer/media notch, four screen edges, app folders and quick search.
- Profile timers, Windows media-session controls, local music/video playback and measured audio visualization.
- Built-in and custom-named profiles, foreground executable activation, optional widgets and OBS hover controls.
- Glass, Dark, Light and Akeno themes; native resizable Settings; 40 JSON-based languages; verified in-app updates.

## Ongoing refinement

Nagi includes a WinUI/Win2D dock, native GPU usage, Core Audio microphone mute, WMI temperature-provider support, optional PresentMon FPS capture, and authenticated OBS WebSocket status/recording control. See [Integrations](INTEGRATIONS.md) for setup and metric definitions. Continue measuring resource usage and refining visible-widget sampling, transitions and incremental search indexing.

Remaining external requirements and physical checks:

1. **Trusted signing:** provide a current code-signing certificate/private key; use scripts/Sign-Nagi.ps1. No certificate is installed on the implementation machine, so the delivered package remains unsigned.
2. **Temperature readings:** run a supported WMI sensor provider. This machine has no provider; no CPU/GPU temperatures are fabricated.
3. **FPS permission:** Windows denies this account's ETW trace. Grant tracing permission or run the separate PresentMon helper with the needed token; keep Nagi unprivileged.
4. **Physical coverage:** monitor hotplug, mixed-DPI monitor-to-monitor drag, exclusive fullscreen games and graphics-driver variants; actual sign-in startup; clean Windows 11 installations; full Narrator/high-contrast/keyboard matrix; media-player command matrix including Spotify where installed.
5. **Future product scope:** Store/MSIX identity. This is not a prerequisite for the implemented controls; custom-named profiles are already implemented.

Media depends on the source app's Windows media-session support. Available transport flags and artwork reflect the source; unsupported controls stay disabled. The local filename index is bounded and may need a narrower selected scope on large folders. Network throughput can include virtual adapters. Configuration and diagnostics remain local.

## A little further

- **macOS — Coming soon.** A future platform direction; no macOS package or delivery date is available.
- **Linux — Coming soon.** A future platform direction; no Linux package or delivery date is available.
- **More languages.** Community contributions through [JSON translations](LANGUAGES.md).
- **Community ideas.** Share requests and improvements through [GitHub Issues](https://github.com/alaaeddinemekkaoui/Nagi/issues).

These directions are not release-date commitments. Update the website roadmap alongside this document when priorities change; only release metadata and changelogs are fetched automatically from GitHub.
