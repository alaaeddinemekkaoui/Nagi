# Hardware, audio and integrations

For optional Open-Meteo weather, output visualization and speaker selection, see [weather and speakers](WELCOME-AND-LAYOUT.md).

Open **Settings → Hardware & audio** for CPU/memory and GPU cards, speakers, output volume, microphone and temperature sensors. Open **Settings → Integrations** for Gaming, PresentMon FPS and OBS Studio. Available values come from actual Windows or provider data; missing samples stay missing. About & diagnostics now uses status cards with technical details in an expander and community thanks at the bottom.

## Automatic Gaming profile

Enable **Integrations → Gaming → Detect games automatically**, then choose **Automatic** in Profiles. Nagi recognizes the active executable under local Steam libraries (including additional libraries), Epic Games manifests and GOG installations. Launcher metadata is cached for one minute; **Rescan game libraries** refreshes it on the next foreground check. No recursive drive scan or online game lookup is used.

An enabled executable rule wins over library detection, and a locked profile always wins. Switching is debounced to avoid rapid changes while Alt-Tabbing. Returning to an unrecognized application returns to the configured default profile after the same debounce. Fullscreen playback and a high frame rate alone never identify a game. Other launchers or nonstandard games can use **Add a Gaming executable rule**. Library detection is directory-based, so an uncommon utility inside a game folder may need an explicit rule for another profile.

**Show Nagi over fullscreen apps** controls visibility independently. Keep the **Performance** widget enabled in Gaming to display CPU, GPU and the optional FPS slot.

## GPU

Windows GPU Engine performance counters provide utilization on supported WDDM drivers, including the Intel UHD adapter used for validation. Per-process contributions are grouped by physical engine, then the busiest engine is reported. Separate engine percentages are not added together. The adapter name comes from Windows. The query is cached and released at shutdown. No driver or administrator access is required when Windows exposes its counters.

This follows [Microsoft's GPU utilization model](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/). Driver/counter availability can differ on other machines.

## Temperatures

Nagi reads Temperature sensors exposed by an existing Libre Hardware Monitor or Open Hardware Monitor WMI provider. CPU/GPU labels follow the provider's hardware identity; ACPI thermal-zone values are never relabeled as CPU temperature. Enable the provider's WMI publishing and leave it running. A hardware-monitor application may itself need administrator access for supported sensors. Nagi does not install/load a kernel driver. This Intel laptop currently has no sensor provider, so CPU/GPU temperatures cannot be verified here.

Provider projects: [Libre Hardware Monitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [Open Hardware Monitor](https://github.com/openhardwaremonitor/openhardwaremonitor).

## FPS

Enable **Integrations → PresentMon FPS → Capture foreground app FPS** to use the bundled, checksum-verified PresentMon 2.6.0. A stacked FPS value appears beside CPU/GPU only when Performance and the profile's FPS option are enabled and a valid foreground measurement arrives. Unavailable or expired FPS leaves no empty slot. Nagi requests short timed traces for the foreground application's process, disables input tracking, bounds output in memory, and uses its own unique trace-session name. Results for a previous foreground process are discarded; turning capture off removes the slot. The displayed value is application presents per second for the dominant swap chain, not screen refresh rate or guaranteed displayed frames. It is not an injected game overlay.

Windows may deny ETW tracing to the current token. The implementation machine returns access denied (exit 6); Nagi stays unprivileged and reports the failure. With the game active, open **FPS help → Start FPS helper…**. Nagi first explains the action; Windows then asks permission to elevate only the helper. An app-started helper stops when FPS is disabled, Nagi closes or that game exits. It does not change user-group membership. A manual helper is also available:

```powershell
# Run only this helper from an Administrator PowerShell if required.
.\Start-NagiFpsCapture.ps1 -GameProcessId 12345
```

Use the actual game PID. Enable FPS in Nagi and focus that game. The helper publishes short snapshots to `%LOCALAPPDATA%\Pulse\Telemetry\foreground-presents.csv` by default. App-started helpers use the current instance's data folder instead; Development and isolated sessions keep their own Telemetry folder. For a manual custom-data session, pass `-DataDirectory '<your Nagi data folder>\Telemetry'`. Nagi accepts only recent frames matching the selected process. Old or unrelated snapshots never become a new rate. Ctrl+C stops a manual helper. Neither path tracks keyboard/mouse events or stops another application's trace session. Elevation occurs only after the user chooses the helper and approves Windows' request.

References: [PresentMon 2.6 console options and metric definitions](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/README-ConsoleApplication.md).

## Microphone

The rail microphone button toggles the default communications microphone using Core Audio. The tooltip identifies the device and mute state. It does not record PCM or grant other apps microphone permission. Read/unavailable states and writing the existing mute state are tested on the real device. The output-volume controls continue to operate separately.

## OBS Studio

Enable OBS's WebSocket server under Tools → WebSocket Server Settings. In Nagi, enable **Connect to OBS**, enter its port/password, then **Save OBS connection**. The client uses WebSocket v5 on `127.0.0.1` only, checks supported requests, and displays connected/recording/streaming/render-FPS status. The Stream profile adds an OBS rail button. Hover it to open OBS, start/stop recording, start/stop streaming or switch scenes. The three scene buttons use the first three OBS scenes by default; choose specific scenes under Settings → Integrations → OBS Studio. Hover and connection checks only read status; output/scene changes require a deliberate button click.

Passwords are protected with Windows DPAPI for the current user and saved separately from settings.json. Logs and diagnostics do not include the password. **Clear saved OBS password** removes that credential. Connection errors, bad passwords, unsupported responses and timeouts are explicit. A request acknowledgement is not reported as successful recording until the observed recording, streaming or program-scene state changes.

References: [OBS WebSocket project](https://github.com/obsproject/obs-websocket), [v5 protocol](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md).

## Distribution and environment coverage

The portable app runs without elevation. `Install-Nagi.ps1` adds a per-user installation and Start menu shortcut; startup stays opt-in. `Close-Nagi.ps1` closes the exact installation gracefully before update/removal. `Sign-Nagi.ps1 -CertificateThumbprint YOUR_THUMBPRINT` signs/timestamps the executable and shipped helper scripts, verifies their signatures, and regenerates the ZIP/checksum. A valid code-signing certificate and private key are required; none is installed here. No self-signed certificate is added to the user's trust store.

Physical monitor hotplug/mixed-DPI hardware, actual Windows sign-in, clean-OS distribution and a full Narrator matrix cannot be certified from this one active desktop session. The validation document distinguishes observed passes from those environment-dependent checks.
