# fs-tools.ps1 - Loader for FsTools module
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot 'Modules\FsTools\FsTools.psd1'
if (Test-Path $modulePath) {
    Import-Module $modulePath -Force -ErrorAction Stop
    Write-Host "FsTools module loaded from $modulePath" -ForegroundColor Green
}
else {
    Write-Warning "FsTools module manifest not found at: $modulePath"
}
