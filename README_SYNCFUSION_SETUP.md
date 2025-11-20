# Syncfusion Local Setup for Wiley-Widget (31.2.2)

This file documents the recommended steps to wire your local Syncfusion 31.2.2 WinUI installation into the project.

## 1) Register local NuGet source (one-time, machine)

Run (PowerShell elevated):

```powershell
$syncPath = "C:\Program Files (x86)\Syncfusion\Essential Studio\WinUI\31.2.2\NuGetPackages"
nuget sources Add -Name "Syncfusion Local WinUI" -Source $syncPath -ConfigFile $env:APPDATA\NuGet\NuGet.Config
```

Or run the included helper script in this repo:

```powershell
pwsh .\scripts\register-syncfusion-nuget.ps1
```

## 2) Install packages (per-project)

In Visual Studio: `Manage NuGet Packages` → Select source "Syncfusion Local WinUI" → install these versions (31.2.2):

- Syncfusion.Grid.WinUI
- Syncfusion.Chart.WinUI
- Syncfusion.Gauge.WinUI
- Syncfusion.BusyIndicator.WinUI
- Syncfusion.Core.WinUI
- Syncfusion.Licensing
- Syncfusion.Shared.WinUI
- Syncfusion.Themes.WinUI

Central versioning is pinned in `Directory.Packages.props` to 31.2.2.

## 3) Theme

App.xaml already includes the Syncfusion Generic theme ResourceDictionary:

```xml
<ResourceDictionary Source="ms-appx:///Syncfusion.Themes.WinUI.WinUI/Generic.xaml" />
```

## 4) License

Set the environment variable `SYNCFUSION_LICENSE_KEY` (User scope) or register in `App.xaml.cs` before components are created:

```csharp
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR_LICENSE_KEY_HERE");
```

## 5) Optional Toolbox

Run Syncfusion toolbox installer:

```powershell
& "C:\Program Files (x86)\Syncfusion\Essential Studio\WinUI\31.2.2\Utilities\Toolbox Installer\SyncfusionToolboxInstaller.exe" /ide:VS2022 /winui
```

## 6) Verification

- `dotnet restore` completes with no errors
- `dotnet build` completes and XAML precompile succeeds
- Visual Studio shows Syncfusion packages under the project references
- Controls render correctly with Fluent theme

## Scripts

- `scripts/register-syncfusion-nuget.ps1` — idempotent registration of local source

---

## Recommended next steps (automated)

The repository includes helper scripts to automate common tasks:

- `scripts/register-syncfusion-nuget.ps1` — idempotent registration of local Syncfusion NuGet source
- `scripts/verify-syncfusion-setup.ps1` — verifies the local source, pins in `Directory.Packages.props`, license presence, and attempts a quick restore/build
- `scripts/install-syncfusion-toolbox.ps1` — (optional) runs the Syncfusion Toolbox installer with elevation to populate the VS Toolbox

Run these in PowerShell (elevated where required):

```powershell
pwsh .\scripts\register-syncfusion-nuget.ps1
pwsh .\scripts\verify-syncfusion-setup.ps1
# optional toolbox installer (requires admin)
pwsh .\scripts\install-syncfusion-toolbox.ps1
```

## Recommended next steps (manual)

1. Verify `Syncfusion Local WinUI` appears in Visual Studio → Tools → NuGet Package Manager → Package Sources.
2. Use Manage NuGet Packages to confirm installed Syncfusion packages are version `31.2.2`.
3. Set the `SYNCFUSION_LICENSE_KEY` environment variable (User scope) or place `license.key` next to the executable.
4. Optionally run the Syncfusion Toolbox installer to populate the Visual Studio toolbox.

---

If you want, I can also add a CI check that verifies package pins and fails if Syncfusion versions diverge from 31.2.2.
