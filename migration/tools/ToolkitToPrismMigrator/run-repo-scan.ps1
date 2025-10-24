param(
    [switch]$FailOnLegacy,
    [switch]$RoslynAnalyze
)

$repoRoot = "c:\Users\biges\Desktop\Wiley_Widget"
$projDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $projDir

# Collect all .cs files excluding bin and obj directories
$files = Get-ChildItem -Path $repoRoot -Recurse -Include *.cs | Where-Object { $_ -and -not ($_.FullName -match "\\bin\\|\\obj\\") } | Select-Object -ExpandProperty FullName

if (-not $files) {
    Write-Error "No .cs files found under $repoRoot"
    Pop-Location
    exit 1
}

Write-Host "Invoking run-scan.ps1 with $($files.Count) files..."

# Call the existing runner script in this folder using named parameter binding to avoid argument parsing issues
if ($FailOnLegacy -and $RoslynAnalyze) {
    & "$projDir\run-scan.ps1" -Files $files -FailOnLegacy -RoslynAnalyze
}
elseif ($FailOnLegacy) {
    & "$projDir\run-scan.ps1" -Files $files -FailOnLegacy
}
elseif ($RoslynAnalyze) {
    & "$projDir\run-scan.ps1" -Files $files -RoslynAnalyze
}
else {
    & "$projDir\run-scan.ps1" -Files $files
}

Pop-Location
