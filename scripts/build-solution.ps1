$logDir = "$PSScriptRoot\..\logs"
if (-not (Test-Path -Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
}
$logFile = Join-Path $logDir "build-solution-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
dotnet build "$PSScriptRoot\..\WileyWidget.sln" --no-restore --verbosity minimal --configuration Debug | Tee-Object -FilePath $logFile -Append
