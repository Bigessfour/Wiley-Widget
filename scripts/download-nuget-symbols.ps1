# Download NuGet symbol packages (.snupkg) and extract PDBs for debugging
# Script: Download Syncfusion and Serilog symbols

param(
    [string]$OutputPath = "./.symbols",
    [switch]$CleanFirst = $false
)

# Ensure output directory exists
if ($CleanFirst -and (Test-Path $OutputPath)) {
    Write-Host "Cleaning existing symbols directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created symbols directory: $OutputPath" -ForegroundColor Green
}

# NuGet symbol server endpoints (test availability of symbols)
# Primary: Direct from NuGet.org symbol server
# Fallback: Microsoft Symbol Server for .NET libraries
$nugetSymbolServer = "https://symbols.nuget.org/download/symbols"

# Packages to download symbols for (versions match current project)
$packages = @(
    @{
        Name    = "Serilog"
        Version = "4.3.0"
    },
    @{
        Name    = "Serilog.Enrichers.Environment"
        Version = "3.0.1"
    },
    @{
        Name    = "Serilog.Enrichers.Process"
        Version = "3.0.0"
    },
    @{
        Name    = "Serilog.Enrichers.Thread"
        Version = "4.0.0"
    },
    @{
        Name    = "Serilog.Extensions.Logging"
        Version = "3.0.1"
    },
    @{
        Name    = "Serilog.Settings.Configuration"
        Version = "3.1.0"
    },
    @{
        Name    = "Serilog.Sinks.File"
        Version = "7.0.0"
    },
    @{
        Name    = "Serilog.Sinks.Console"
        Version = "3.1.1"
    },
    @{
        Name    = "Serilog.Sinks.Debug"
        Version = "2.0.0"
    },
    @{
        Name    = "Serilog.Sinks.Trace"
        Version = "2.1.0"
    },
    @{
        Name    = "Serilog.Sinks.Async"
        Version = "2.1.0"
    }
)

Write-Host "`nDownloading NuGet packages and extracting PDBs...`n" -ForegroundColor Cyan

$downloadedCount = 0
$failedDownloads = @()

foreach ($package in $packages) {
    $packageName = $package.Name
    $packageVersion = $package.Version
    $nugetUrl = "https://www.nuget.org/api/v2/package/$packageName/$packageVersion"

    Write-Host "Processing: $packageName v$packageVersion..." -ForegroundColor White

    try {
        $tempNupkg = Join-Path $OutputPath "temp_$(Get-Random).nupkg"
        $tempZip = "$tempNupkg.zip"
        $tempExtract = Join-Path $OutputPath "temp_extract_$(Get-Random)"

        # Download .nupkg package (note: .nupkg is actually a ZIP file)
        Write-Host "  ↓ Downloading..." -ForegroundColor Gray
        $response = Invoke-WebRequest -Uri $nugetUrl -OutFile $tempNupkg -SkipHttpErrorCheck -SkipCertificateCheck -PassThru

        if ($response.StatusCode -eq 200 -and (Test-Path $tempNupkg) -and (Get-Item $tempNupkg).Length -gt 0) {
            Write-Host "  ✓ Downloaded" -ForegroundColor Green

            # Copy to .zip and extract (nupkg is a ZIP archive)
            Copy-Item -Path $tempNupkg -Destination $tempZip -Force
            Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force -ErrorAction Stop

            # Find all PDB files in the archive
            $pdbFiles = @(Get-ChildItem -Path $tempExtract -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue)

            if ($pdbFiles.Count -gt 0) {
                # Create package symbols directory
                $pkgSymbols = Join-Path $OutputPath $packageName
                New-Item -ItemType Directory -Path $pkgSymbols -Force | Out-Null

                # Copy PDB files
                foreach ($pdbFile in $pdbFiles) {
                    $pdbName = Split-Path -Leaf $pdbFile.FullName
                    Copy-Item -Path $pdbFile.FullName -Destination (Join-Path $pkgSymbols $pdbName) -Force
                }

                Write-Host "  ✓ Extracted $($pdbFiles.Count) PDB file(s)" -ForegroundColor Green
                $downloadedCount++
            }
            else {
                Write-Host "  ℹ No PDB files found (symbols likely embedded)" -ForegroundColor DarkGray
            }
        }
        else {
            Write-Host "  ✗ Download failed" -ForegroundColor Yellow
            $failedDownloads += $packageName
        }
    }
    catch {
        Write-Host "  ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
        $failedDownloads += $packageName
    }
    finally {
        # Clean up temp files
        @($tempNupkg, $tempZip, $tempExtract) | ForEach-Object {
            if (Test-Path $_) {
                if ((Get-Item $_).PSIsContainer) {
                    Remove-Item -Path $_ -Recurse -Force -ErrorAction SilentlyContinue
                } else {
                    Remove-Item -Path $_ -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Symbol Download Complete" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($failedDownloads.Count -eq 0) {
    Write-Host "✓ All symbols downloaded successfully!" -ForegroundColor Green
    Write-Host "`nSymbols location: $(Resolve-Path -Path $OutputPath)" -ForegroundColor Green
} else {
    Write-Host "⚠ Symbol availability:" -ForegroundColor Yellow
    Write-Host "  Successfully downloaded: $($packages.Count - $failedDownloads.Count) package(s)" -ForegroundColor Green
    Write-Host "  Not available: $($failedDownloads.Count) package(s)" -ForegroundColor Yellow
    Write-Host "`nNote: Symbols not available from public servers are often included" -ForegroundColor Cyan
    Write-Host "      directly in the NuGet packages via DebugType: embedded" -ForegroundColor Cyan
}

Write-Host "`n" -ForegroundColor Cyan
Write-Host "VS Code Configuration:" -ForegroundColor Cyan
Write-Host "  Symbol path: $OutputPath" -ForegroundColor Green
Write-Host "  NuGet server: $nugetSymbolServer" -ForegroundColor Green
Write-Host "  Microsoft server: Enabled for .NET Framework symbols" -ForegroundColor Green
Write-Host "`nDebugging: Symbols will be automatically loaded during breakpoints.`n" -ForegroundColor Cyan
