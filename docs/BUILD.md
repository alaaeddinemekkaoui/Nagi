# Build and distribution

Use Windows 11 x64 with the SDK pinned in `global.json`. Windows SDK 10.0.26100 is installed on the implementation machine. NuGet restores the Windows App SDK compiler and native dependencies. No Electron, browser, backend, administrator token or runtime download is required by the published application.

```powershell
dotnet --info
dotnet restore Pulse.sln
dotnet build Pulse.sln -c Release
dotnet test Pulse.sln -c Release --no-build --logger trx
.\scripts\Run-Nagi.ps1 -Configuration Release
```

If building only behavior tests on another OS, use `dotnet test tests/Pulse.Tests/Pulse.Tests.csproj`; the UI and Windows integration tests require Windows. Audio/microphone tests write existing values back unchanged to verify the setters. Tests never send media controls or launch configured shortcuts. The startup registry test temporarily registers/removes a per-user value and restores the original value/type. Hotkey tests temporarily register an unused Ctrl+Alt+Shift+F13–F24 key and release it immediately.

```powershell
.\scripts\Publish-Nagi.ps1
```

This invokes `dotnet publish src/Pulse.App/Pulse.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/Nagi-win-x64`, adds installer/uninstaller scripts and README, then compresses all output and prints its SHA-256 hash. Keep all DLLs and resource folders together. A single EXE is not requested; a self-contained folder avoids extraction and app identity complications.

The application project explicitly adds its compiled XAML (`.xbf`), app resource index (`Pulse.App.pri`) and assets to the publish graph. The standard unpackaged publish path can omit these despite a successful command; see [Windows App SDK issue 6720](https://github.com/microsoft/WindowsAppSDK/issues/6720). The package script checks required resources before creating the ZIP.

Quit a running copy from the output folder before rebuilding/publishing that same folder. For interactive developer checks, `Smoke-Pulse.ps1 -PulseProcessId <pid>` exercises Nagi, and `Smoke-PulseTray.ps1 -PulseProcessId <pid> -Quit` clicks the actual English Windows 11 overflow icon/menu and exits through Quit. The latter also tests Settings recovery after hiding. These scripts change transient UI/profile state and are intended for a development session.

Installation from an extracted package:

```powershell
.\Install-Nagi.ps1
```

This creates `%LOCALAPPDATA%\Programs\Nagi\Nagi.exe` and a Start menu shortcut. It does not enable startup automatically. Do not install from inside the installation directory. Installer scripts operate on the explicit per-user Nagi directory, and uninstall validates that directory before recursive removal. Source builds and user settings are not removed.

Uninstall:

```powershell
& "$env:LOCALAPPDATA\Programs\Nagi\Uninstall-Nagi.ps1"
```

The package is unsigned. A production installer should be signed and tested on clean Windows 11 machines before broad distribution. No MSIX identity, Store submission or update service is configured.

For Nagi 1.3 use `Smoke-PulseEdge.ps1 -PulseProcessId <pid> -Screenshots` to check hover/minimal/pin/expand behavior on both sides. This helper changes the selected side, so save and restore your development preferences. Disable outside dismissal/fullscreen hiding temporarily for deterministic automation, then restore those preferences before normal use.

For Nagi 1.4, `Smoke-PulseLua.ps1 -PulseProcessId <pid> -Screenshots` validates the native Lua editor, apply/reload/error recovery, section/label changes and opt-out. It writes `%LOCALAPPDATA%\Pulse\scripts\ui.lua` and preferences. Back up and restore both before using it against a development instance. The portable publish includes the MoonSharp runtime through NuGet and its notice in `docs/MoonSharp-LICENSE.txt`; no standalone Lua installation is needed. See `docs/LUA-UI.md` for the scripting API.

For 1.7 the package checks Win2D managed/native interop, PresentMon and its full license in addition to XAML resources. Provider configuration and exact constraints are in INTEGRATIONS.md. Installer update/removal closes the matching installation gracefully through Close-Nagi.ps1; it does not force-kill unrelated copies. A certificate-backed release can be created with Sign-Nagi.ps1 -CertificateThumbprint YOUR_THUMBPRINT. No certificate is available in this development session, so the generated portable package remains unsigned.
