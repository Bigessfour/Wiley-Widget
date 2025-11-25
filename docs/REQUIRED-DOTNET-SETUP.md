**.NET SDK Requirements**

- **Required SDK:** .NET 10 (example: `10.0.100`)
- **Required MSBuild:** MSBuild major version **18** or newer (bundled with .NET 10 SDK)

Why: The repository targets `net10.0` and requires MSBuild v18+ to correctly import and run custom targets such as `CompileXaml`.

Quick verification (run from repo root):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\maintenance\verify-dotnet-sdk.ps1
```

If the script fails, install the .NET 10 SDK for your OS:

- Windows installer and instructions: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

After installing, re-run the verification script and then run your build (or `dotnet --version` / `dotnet --info` to confirm).
