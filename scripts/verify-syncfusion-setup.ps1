<#
Verify Syncfusion local NuGet source, package versions, and license presence
#>

param()

$syncSourceName = 'Syncfusion Local WinUI'
$expectedPath = 'C:\Program Files (x86)\Syncfusion\Essential Studio\WinUI\31.2.2\NuGetPackages'

Write-Host "== Syncfusion Setup Verification =="

# 1) Check nuget source
$sources = dotnet nuget list source --format short | Out-String
if ($sources -match [regex]::Escape($expectedPath)) {
    Write-Host "[OK] Local NuGet source registered: $expectedPath"
} else {
    Write-Host "[WARN] Local NuGet source NOT registered. Run: pwsh .\scripts\register-syncfusion-nuget.ps1" -ForegroundColor Yellow
}

# 2) Check Directory.Packages.props versions
$dp = Join-Path (Get-Location) 'Directory.Packages.props'
if (Test-Path $dp) {
    $content = Get-Content $dp -Raw
    $syncok = $true
    foreach ($pkg in @('Syncfusion.Licensing','Syncfusion.Grid.WinUI','Syncfusion.Chart.WinUI','Syncfusion.Core.WinUI','Syncfusion.Themes.WinUI')) {
        if ($content -notmatch "$pkg.*31.2.2") { $syncok = $false; Write-Host "[WARN] $pkg not pinned to 31.2.2 in Directory.Packages.props" -ForegroundColor Yellow }
    }
    if ($syncok) { Write-Host "[OK] Directory.Packages.props pins Syncfusion packages to 31.2.2" }
} else { Write-Host "[WARN] Directory.Packages.props not found" -ForegroundColor Yellow }

# 3) Check license env var or license.key
$envKey = [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY','User')
if (![string]::IsNullOrWhiteSpace($envKey)) { Write-Host "[OK] SYNCFUSION_LICENSE_KEY is set (User scope)" } else {
    $exeLicense = Join-Path (Get-Location) 'license.key'
    if (Test-Path $exeLicense) { Write-Host "[OK] license.key found beside repo: $exeLicense" } else { Write-Host "[WARN] No SYNCFUSION_LICENSE_KEY env var or license.key file found" -ForegroundColor Yellow }
}

# 4) Attempt dotnet restore and build (quick validation)
Write-Host "Attempting dotnet restore/build (may take a moment)..."
try {
    dotnet restore Wiley-Widget.csproj | Write-Host
    dotnet build Wiley-Widget.csproj -c Debug | Write-Host
} catch {
    Write-Host "[ERROR] Build/restore failed. Inspect console output." -ForegroundColor Red
}

Write-Host "== Verification complete =="
