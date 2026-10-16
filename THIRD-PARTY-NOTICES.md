# Third-party components

- **NSIS 3.12 / Modern UI 2** supplies the setup/uninstaller runtime. Full notices are included in [docs/NSIS-LICENSE.txt](docs/NSIS-LICENSE.txt). Source and terms: https://nsis.sourceforge.io/License.
- **Open-Meteo** provides optional model weather conditions and geocoding. Attribution: https://open-meteo.com/ and location data https://www.geonames.org/. Data is CC BY 4.0; attribution appears in the app. Data is requested when enabled, not bundled.
- Speaker interop references: Windows SDK mmdeviceapi.h and https://github.com/frgnca/AudioDeviceCmdlets/blob/master/SOURCE/IPolicyConfig.cs. Default switching relies on the Windows PolicyConfig service.

- **WinUI / Windows App SDK 1.8** and **Win2D 1.4.0** — Microsoft UI and Direct2D rendering libraries. Microsoft runtime redistributable terms and MIT-licensed source apply. Sources: https://github.com/microsoft/WindowsAppSDK and https://github.com/microsoft/Win2D.
- **PresentMon 2.6.0 x64** — Intel's ETW graphics-presentation analysis tool, MIT license. The full license travels with `Assets/Tools/PresentMon-LICENSE.txt`. Official release: https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0. Bundled executable SHA256: `b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af`.
- **System.Management 10.0.0 / .NET runtime** — Microsoft/.NET Foundation, MIT license: https://github.com/dotnet/runtime.
- **MoonSharp 2.0** — embedded Lua interpreter; BSD 3-Clause license: https://github.com/moonsharp-devs/moonsharp.

Pulse does not bundle a hardware-monitor kernel driver, OBS Studio, or a temperature sensor application. Those integrations connect to user-provided software and retain its own licensing and system requirements.
