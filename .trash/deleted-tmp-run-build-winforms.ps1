$ws = 'c:\Users\biges\Desktop\Wiley-Widget'
$logDir = Join-Path $ws 'logs'
if(-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$logFile = Join-Path $logDir ("build-winforms-$(Get-Date -Format 'yyyyMMdd-HHmmss').log")
Write-Output "Building WinForms project and writing output to: $logFile"

dotnet build "$ws\WileyWidget.WinForms\WileyWidget.WinForms.csproj" /property:GenerateFullPaths=true /consoleloggerparameters:"NoSummary;Verbosity=minimal" /p:DebugType=portable /p:DebugSymbols=true | Tee-Object -FilePath $logFile -Append

Write-Output "Finished; check log file: $logFile"