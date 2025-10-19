param(
    [string]$Project = "WileyWidget.Data/WileyWidget.Data.csproj",
    [string]$StartupProject = "WileyWidget.csproj"
)

$ErrorActionPreference = "Stop"

Write-Information "=== EF Drift Verification ===" -InformationAction Continue
Write-Information "Project: $Project" -InformationAction Continue
Write-Information "Startup Project: $StartupProject" -InformationAction Continue

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $repoRoot

$driftFolderRelative = "Migrations/__DriftCheck"
$projectDirectory = Split-Path -Parent $Project
$driftFolder = Join-Path $projectDirectory $driftFolderRelative

if (Test-Path $driftFolder) {
    Write-Information "Removing previous drift check artifacts at $driftFolder" -InformationAction Continue
    Remove-Item -Path $driftFolder -Recurse -Force
}

$arguments = @(
    "ef", "migrations", "add", "__DriftCheck",
    "--project", $Project,
    "--startup-project", $StartupProject,
    "--output-dir", $driftFolderRelative,
    "--no-build"
)

Write-Information "Running: dotnet $($arguments -join ' ')" -InformationAction Continue

$process = Start-Process -FilePath "dotnet" -ArgumentList $arguments -NoNewWindow -PassThru -Wait

$driftFiles = @()
if (Test-Path $driftFolder) {
    $driftFiles = Get-ChildItem -Path $driftFolder -Recurse -File -ErrorAction SilentlyContinue
}

try {
    if ($process.ExitCode -ne 0) {
        throw "dotnet ef exited with code $($process.ExitCode)."
    }

    if ($driftFiles.Count -gt 0) {
        $fileList = $driftFiles | ForEach-Object { $_.FullName }
        $message = "Pending EF Core model changes detected. Generated files: `n" + ($fileList -join "`n")
        throw $message
    }

    Write-Output "✅ No pending EF Core model changes detected."
}
finally {
    if (Test-Path $driftFolder) {
        Write-Information "Cleaning up drift check artifacts." -InformationAction Continue
        Remove-Item -Path $driftFolder -Recurse -Force
    }
}
