# Remaining environment coverage

Nagi V1.0 includes a WinUI/Win2D vertical rail, native GPU usage, Core Audio microphone mute, WMI temperature-provider support, optional PresentMon FPS capture, and authenticated OBS WebSocket status/recording control. See INTEGRATIONS.md for setup and metric definitions.

Remaining external requirements and physical checks:

1. **Trusted signing:** provide a current code-signing certificate/private key; use scripts/Sign-Nagi.ps1. No certificate is installed on the implementation machine, so the delivered package remains unsigned.
2. **Temperature readings:** run a supported WMI sensor provider. This machine has no provider; no CPU/GPU temperatures are fabricated.
3. **FPS permission:** Windows denies this account's ETW trace. Grant tracing permission or run the separate PresentMon helper with the needed token; keep Nagi unprivileged.
4. **Physical coverage:** monitor hotplug, mixed-DPI monitor-to-monitor drag, exclusive fullscreen games and graphics-driver variants; actual sign-in startup; clean Windows 11 installations; full Narrator/high-contrast/keyboard matrix; media-player command matrix including Spotify where installed.
5. **Future product scope:** arbitrary user-created profiles, Store/MSIX identity. These are not prerequisites for the implemented controls.

Media depends on the source app's Windows media-session support. Available transport flags and artwork reflect the source; unsupported controls stay disabled. The local filename index is bounded and may need a narrower selected scope on large folders. Network throughput can include virtual adapters. Configuration and diagnostics remain local.
