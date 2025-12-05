# NuGet Management Scripts

PowerShell scripts for managing the Wiley Widget private NuGet feed.

## Scripts

| Script | Description |
|--------|-------------|
| `start-server.ps1` | Start/stop the BaGet NuGet server in Docker |
| `push-package.ps1` | Push .nupkg files to the private feed |
| `list-packages.ps1` | List packages available on the private feed |

## Quick Start

```powershell
# 1. Set your API key (one-time setup)
$env:NUGET_API_KEY = [System.Guid]::NewGuid().ToString("N")

# 2. Start the server
.\scripts\nuget\start-server.ps1

# 3. Push a Syncfusion patch
.\scripts\nuget\push-package.ps1 -PackagePath ".\Syncfusion.SfChart.WinForms.31.2.17.nupkg"

# 4. List packages
.\scripts\nuget\list-packages.ps1
```

## VS Code Tasks

Use Command Palette (Ctrl+Shift+P) → "Tasks: Run Task":

- `nuget:start-server` - Start the NuGet server
- `nuget:stop-server` - Stop the NuGet server  
- `nuget:push-package` - Push a single package (prompts for path)
- `nuget:push-syncfusion-patches` - Push all packages from `packages/local/syncfusion-patches/`
- `nuget:list-packages` - List all packages on the feed

## Server Details

- **URL**: http://localhost:5000/
- **API Endpoint**: http://localhost:5000/v3/index.json
- **Container**: `wiley-nuget-server` (BaGet image)
- **Volumes**: `nuget-packages`, `nuget-db`

## See Also

- `tools/nuget-server/README.md` - Full server documentation
- `secrets/nuget_api_key.example` - API key setup guide
