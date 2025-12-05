# Wiley Widget Private NuGet Server (BaGet)

A lightweight, Docker-based private NuGet server for hosting Syncfusion patches and custom packages.

## Quick Start

### Start the Server

```powershell
# From repository root
docker-compose up -d nuget-server

# Or use the VS Code task: "nuget:start-server"
```

### Push a Package

```powershell
# Set your API key (one-time setup)
$env:NUGET_API_KEY = "your-secure-api-key-here"

# Push a package
dotnet nuget push "path/to/package.nupkg" `
    --source http://localhost:5000/v3/index.json `
    --api-key $env:NUGET_API_KEY
```

### List Packages

```powershell
# Browse packages at:
# http://localhost:5000/

# Or via CLI:
dotnet nuget list source WileyPrivate
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `NUGET_API_KEY` | API key for push/delete operations | Required |
| `BAGET_DB_PATH` | SQLite database path | `baget.db` |

### Package Storage

Packages are stored in `./packages/` directory within the container, mounted to `nuget-packages` Docker volume for persistence.

## Integration with Wiley Widget

The private feed is configured in `NuGet.config` with package source mapping:

```xml
<packageSourceMapping>
    <packageSource key="WileyPrivate">
        <package pattern="Syncfusion.*" />
    </packageSource>
    <packageSource key="nuget.org">
        <package pattern="*" />
    </packageSource>
</packageSourceMapping>
```

This ensures Syncfusion patches from the private feed take priority over nuget.org versions.

## Syncfusion Patch Workflow

1. **Download patch** from Syncfusion dashboard or MCP Server
2. **Push to private feed**:
   ```powershell
   .\scripts\nuget\push-package.ps1 -PackagePath ".\Syncfusion.SfChart.WinForms.31.2.17.nupkg"
   ```
3. **Update version** in `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="Syncfusion.SfChart.WinForms" Version="31.2.17" />
   ```
4. **Restore and build**:
   ```powershell
   dotnet restore
   dotnet build
   ```

## Troubleshooting

### Server Not Starting
```powershell
# Check container logs
docker logs wiley-nuget-server

# Verify port availability
netstat -an | Select-String ":5000"
```

### Push Fails with 401 Unauthorized
- Verify `NUGET_API_KEY` environment variable is set
- Check API key matches `appsettings.json` configuration

### Package Not Found After Push
- Clear local NuGet cache: `dotnet nuget locals all --clear`
- Verify package appears at http://localhost:5000/

## Security Notes

- **Never commit** your actual API key to version control
- Use environment variables or secrets management
- For production, enable HTTPS with proper certificates
- See `secrets/nuget_api_key.example` for setup guidance

## API Key Clarification (Syncfusion vs NuGet)

**Important**: The NuGet API key used here is **not** a Syncfusion-provided key. It is a user-defined value that you create yourself for securing your private NuGet feed.

| Key Type | Purpose | Source |
|----------|---------|--------|
| **NuGet API Key** (`NUGET_API_KEY`) | Authenticates `nuget push`/`delete` operations to your private server | You create it (e.g., a GUID or secure password) |
| **Syncfusion License Key** | Activates Syncfusion controls at runtime | Provided by Syncfusion (via dashboard/MCP) |

### How It Works

1. **You define the API key** when setting up BaGet (via `appsettings.json` or environment variable)
2. **You use that same key** when pushing packages:
   ```powershell
   dotnet nuget push "Syncfusion.SfChart.WinForms.31.2.17.nupkg" `
       --source http://localhost:5000/v3/index.json `
       --api-key $env:NUGET_API_KEY
   ```
3. **Syncfusion's official feeds** (nuget.org) don't require any API key for restore—only your license key for runtime activation

### Generating a Secure API Key

```powershell
# Generate a cryptographically secure API key
[System.Guid]::NewGuid().ToString("N")
# Example output: a1b2c3d4e5f6789012345678abcdef01

# Or use a longer random string
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

### Configuration Options

**For BaGet (Docker)** — Set via environment variable in `docker-compose.yml`:
```yaml
environment:
  - ApiKey=${NUGET_API_KEY:-defaultdevkey123}
```

**For NuGet.Server (IIS)** — Set in `web.config`:
```xml
<appSettings>
  <add key="apiKey" value="your-strong-password-here" />
  <add key="requireApiKey" value="true" />
</appSettings>
```

If you're only consuming packages (restore) and not pushing, no API key is needed—just ensure `NuGet.config` has the correct source URL.
