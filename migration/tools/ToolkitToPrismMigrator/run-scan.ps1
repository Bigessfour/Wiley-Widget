param(
    [string[]]$Files = @("..\..\..\src\ViewModels\MunicipalAccountViewModel.cs"),
    [switch]$FailOnLegacy,
    [switch]$RoslynAnalyze
)

$projDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $projDir

dotnet build -c Release | Out-Host
# Find the built DLL under bin\Release\net*\ToolkitToPrismMigrator.dll
# Prefer net9.0 output if present
$dlls = Get-ChildItem -Path (Join-Path $projDir 'bin\Release\net9.0') -Filter 'ToolkitToPrismMigrator.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $dlls) {
    $dlls = Get-ChildItem -Path (Join-Path $projDir 'bin\Release') -Recurse -Filter 'ToolkitToPrismMigrator.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($null -eq $dlls) {
    Write-Error "Built DLL not found under bin\Release. Expected ToolkitToPrismMigrator.dll"
    Pop-Location
    exit 2
}
$exe = $dlls.FullName

if (-not (Test-Path $exe)) {
    Write-Error "Build failed or output not found: $exe"
    Pop-Location
    exit 2
}

$argList = $Files
if ($FailOnLegacy) { $argList += '--fail-on-legacy' }
if ($RoslynAnalyze) { $argList += '--roslyn-analyze' }

# Run with dotnet
$cmd = @('dotnet', $exe) + $argList
Write-Host "Running: $($cmd -join ' ')"
$startArgs = @($exe) + $argList
$process = Start-Process -FilePath dotnet -ArgumentList $startArgs -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) { Write-Error "Tool exited with $($process.ExitCode)"; Pop-Location; exit $process.ExitCode }

Pop-Location
