# Delivery status

Legend: **Verified** = automated or observed runtime pass on the implementation machine; **Implemented** = source/build complete with specific broader checks remaining; **Unavailable** = explicitly unsupported, no fabricated behavior.

| Requirement | State | Evidence / remaining coverage |
| --- | --- | --- |
| Quick search / apps / files / useful actions | Verified | 149 automated cases; live Alt+Space, 276 installed apps, filename fixture, calculator, filters, Escape/reopen and Settings navigation; folder scope configurable |
| Akeno / live media and audio | Verified | 120 automated cases; live Akeno/audio refresh/hover/screen-fit and both-edge interaction checks; actual player artwork and playback matrix pending |
| Lua UI editor / presets / reload | Verified | 25 new automated cases plus live editor/apply/reload/error/opt-out checks; declarative appearance API documented in docs/LUA-UI.md |
| Battery percentage / compact logo | Verified | Native GetSystemPowerStatus with sentinel/charging tests; live logo/drag/battery/pin/expand checks pass on both screen edges |
| Vertical edge rail / hover / pin / expand | Verified | Interaction policy tests, edge geometry tests, live pointer smoke at 125% DPI |
| CPU / memory / media hover previews | Verified | Physical-pointer dwell, entry, exit, Show more and foreground-focus smoke; four native anchor tests |
| Shared pill/panel/dashboard state | Verified | One window/view model/services, runtime inspection |
| Expand/collapse and hide/show shortcuts | Verified | Actual Ctrl+Alt+Space / Ctrl+Alt+H smoke; independent native conflict/release tests |
| Native tray show/hide/settings/quit | Verified | Actual overflow icon and menu, real mouse selection, hidden-window recovery and clean quit |
| Tray registration / recreation / cleanup | Verified | Native shell tests with unique icon IDs; actual Explorer restart remains a manual matrix item |
| Acrylic glass / compact overview | Verified | Live redesigned UI; native material reports Active; dark/light/high-contrast resources and opaque option |
| Smooth transitions / interruption | Verified | Native window springs plus GPU composition; convergence/retarget/frame-pacing tests |
| Position persistence / offscreen recovery | Verified | Atomic settings tests, native missing-monitor/clamping test |
| Multiple monitors / DPI changes | Implemented | Monitor-aware Win32 implementation; physical matrix pending |
| CPU / RAM / total and available memory | Verified | Live metrics and native tests |
| Network throughput | Verified | Live byte deltas, honest first-sample state |
| Foreground identity and process status | Verified | Real Win32 PID/name/path, live inspection |
| Media metadata | Verified | Live Windows session metadata |
| Media artwork | Verified | Actual cover inspected in compact rail, hover preview and overview; broader player matrix remains |
| Media transport | Implemented | Supported GSMTC commands and capability flags; playback-changing manual checks pending |
| Master audio volume / mute | Verified | Core Audio read and idempotent setter tests |
| Configurable validated shortcuts | Verified | UI dialogs, rule/action validators; external launches not broadly exercised |
| Work / Gaming / Focus / Automatic | Verified | Core tests and live manual profile switching |
| Rules, priorities, debounce, locks | Verified | Core tests, persistent UI edits |
| Settings / corrupt / future schema recovery | Verified | Tests including locked file recovery |
| Cancellation / clean shutdown | Verified | Runtime graceful exit and lifecycle log |
| Single instance | Verified | Live second-launch smoke |
| Startup with Windows | Implemented | Native per-user Run registration/removal round-trip passes with restoration; actual sign-in check pending |
| Diagnostics / error feedback | Verified | Live diagnostic UI and bounded logs |
| Dark / Light and theme persistence | Verified | Live selection/save/restart restoration |
| System theme / reduced motion | Implemented | Native preferences; full accessibility matrix pending |
| Privacy / minimum privileges | Verified | Local-only design, asInvoker manifest, no screenshots, keyboard recording or outbound telemetry; mouse-button state is read for dragging |
| Solution build / tests | Verified | Release zero warnings/errors; 167 passing tests |
| Portable package | Verified | Published binary launches and passes UI smoke; required PRI/XBF integrity checks |
| Installer / uninstaller scripts | Verified | Actual per-user install, required resources, Start menu shortcut, installed launch, graceful uninstall and preference retention pass; trusted signing requires a certificate |
| GPU usage / microphone mute | Verified | Real Intel UHD PDH samples; real communications-microphone read/idempotent mute tests and live rail state |
| Temperature providers | Implemented | Existing LHM/OHM WMI sensors; no sensor provider is installed on this machine |
| FPS capture / helper | Implemented | Bundled verified PresentMon; parser/freshness/PID tests; native trace currently denied by Windows permission |
| OBS WebSocket / recording | Verified | Auth/error/state-ack tests; real isolated OBS 32.2.2 authenticated status and empty-scene start/status/stop pass |

See `docs/VALIDATION.md` for exact scope and `docs/ROADMAP.md` for the next milestone.
