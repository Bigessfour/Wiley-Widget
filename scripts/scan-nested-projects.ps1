[CmdletBinding()]
param(
    [string]$Workspace = 'C:\Users\biges\Desktop\Wiley_Widget'
)
$ErrorActionPreference = 'Stop'
$rootProj = Join-Path $Workspace 'WileyWidget.csproj'
$projects = @(Get-ChildItem -Path $Workspace -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $rootProj })
Set-StrictMode -Version Latest
Write-Output 'Scanning for nested *.csproj (excluding root)...'
if ($projects.Count -gt 0) { $projects | ForEach-Object { Write-Output $_.FullName } } else { Write-Output 'None' }
Write-Output ("Found: $($projects.Count)")
