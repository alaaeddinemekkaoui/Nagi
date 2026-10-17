# Build and release Nagi v0.1

Use Windows 11 x64 and the .NET SDK pinned in `global.json`. NuGet restores the Windows App SDK compiler and native dependencies. The self-contained app needs no runtime download on the target PC.

```powershell
dotnet restore Pulse.sln
dotnet build Pulse.sln -c Release
dotnet test Pulse.sln -c Release --no-build --logger trx
./scripts/Publish-Nagi.ps1 -NoRestore
./scripts/Build-NagiInstaller.ps1 -SkipPublish
```

## Release outputs

Every publish produces:

- `artifacts/Nagi-win-x64/`: self-contained app folder, including XAML resources, assets, provider tools, licenses, docs and the update helper.
- `artifacts/Nagi-win-x64.zip` and `.sha256`.
- **`Nagi-Portable.exe` in the project root**, plus `.sha256`, with matching copies under `artifacts/`.

`Build-NagiInstaller.ps1 -SkipPublish` creates `artifacts/Nagi-Setup.exe` and `.sha256`. Both EXE formats use NSIS 3.12; install its compiler or pass `-CompilerPath` to the build helper. It recognizes `artifacts/tooling/nsis-3.12/makensis.exe`. [NSIS download](https://nsis.sourceforge.io/Download).

`Build-NagiPortable.ps1 -SkipPublish` rebuilds just the portable EXE from the published payload and ZIP. Normal publish always refreshes it, so every release build leaves an executable ready to open in the project root. Generated binaries are Git-ignored and uploaded as release assets.

## Portable EXE

Open `Nagi-Portable.exe` directly. It contains all app/runtime files, prepares them in `%LOCALAPPDATA%/Nagi/Portable/<payload-id>`, and starts Nagi without installation, shortcuts or startup registration. Later launches reuse those files, including updates installed by the app. Arguments such as `--data-dir` are forwarded unchanged. Preferences use the normal app data folder unless an isolated directory is supplied.

## Release identity and GitHub

The public release is **Nagi**, version **v0.1**, tag **`v0.1`**. `src/Pulse.App/Pulse.App.csproj` supplies version `0.1.0` to the app and EXE metadata. The changelog has one Nagi v0.1 section. Publish generates `RELEASE_README.md` from the README using links pinned to `v0.1`; `-ReleaseTag` supports a future tag.

Upload the portable EXE, ZIP, setup and their SHA256 files. Use `RELEASE_README.md` as the release body. In-app updates require the Windows x64 ZIP and a GitHub SHA256 digest or checksum asset. `Apply-NagiUpdate.ps1` stages replacement outside the running app and backs up replaced files for rollback.

## Installer and signing

Setup installs in `%LOCALAPPDATA%/Programs/Nagi` without administrator rights. It closes that installation gracefully, creates the Start menu entry and registers per-user removal. Startup stays opt-in. Removal preserves preferences and unknown files; it removes only files listed in the published payload.

The app and wrappers are unsigned in this workspace. `Sign-Nagi.ps1 -CertificateThumbprint YOUR_THUMBPRINT` signs the app/helpers when a current certificate/private key is available. Sign finished setup/portable EXEs as part of a certificate-backed distribution.

## Desktop verification

Close any copy from an output folder before rebuilding it. Helpers use dedicated data directories where supported:

```powershell
./scripts/Smoke-NagiWorkspaces.ps1 -ExecutablePath <exe>
./scripts/Smoke-NagiAppFolder.ps1 -ExecutablePath <exe>
./scripts/Smoke-NagiAppFolder.ps1 -ExecutablePath <exe> -Orientation Vertical
./scripts/Smoke-NagiUpdate.ps1
```

Use `-DocumentationImages` with the horizontal app-folder check to refresh the screenshots. The welcome helper checks onboarding, themes, layouts, weather and speakers. `--data-dir <absolute-path>` isolates normal preferences. Coverage limits are in [Validation](VALIDATION.md) and [Integrations](INTEGRATIONS.md).

To keep a running development copy untouched, pass `-PackageDirectory artifacts/Nagi-v0.1-win-x64` to publish and the installer builder. `Smoke-NagiPortable.ps1` verifies the root EXE from a folder containing no DLLs, with isolated preferences, cache reuse and single-instance behavior.
