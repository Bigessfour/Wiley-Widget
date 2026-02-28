# Packaging & Signing (WinForms)

This guide provides a minimal, repeatable path for creating release artifacts with signing.

## Prerequisites

- Build in `Release` configuration.
- Keep private certificates out of source control (`.pfx`, `.snk`, `.p12`).
- Store production keys in a secure vault/HSM (see `signing/README.md`).

## Baseline Release Publish

Use the existing build script to produce publish output:

```powershell
pwsh ./scripts/build.ps1 -Publish -SelfContained -Runtime win-x64
```

Output is generated under standard .NET publish folders for the WinForms project.

## ClickOnce (Folder-based)

Use `dotnet publish` with ClickOnce-compatible properties:

```powershell
dotnet publish src/WileyWidget.WinForms/WileyWidget.WinForms.csproj \
  -c Release \
  -p:PublishProtocol=ClickOnce \
  -p:PublishDir=artifacts/clickonce/ \
  -p:GenerateManifests=true \
  -p:Install=true \
  -p:UpdateEnabled=true
```

If you sign ClickOnce manifests, include your certificate properties via secure CI variables (do not hard-code secrets).

## MSI (Recommended via WiX Toolset)

Create MSI from published files:

1. Publish app files (`Release`, x64) to a staging folder.
2. Author a WiX installer project that references the staged output.
3. Build MSI in CI from that WiX project.
4. Sign the final MSI and binaries.

Recommended command sequence (example):

```powershell
# 1) publish app payload
dotnet publish src/WileyWidget.WinForms/WileyWidget.WinForms.csproj -c Release -o artifacts/msi/payload

# 2) build wix installer project (example path)
dotnet build installer/WileyWidget.Installer.wixproj -c Release
```

## Code Signing (Executable + MSI)

Sign release outputs as a final step:

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a <path-to-file>
```

Apply signing to:

- Primary executable(s)
- Supporting DLLs (if required by policy)
- Installer package (`.msi`)

## CI Recommendations

- Keep certificate/thumbprint/password in secret storage.
- Fail build if signing step fails.
- Archive unsigned and signed artifacts separately for auditability.
