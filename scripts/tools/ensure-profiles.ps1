#Requires -Version 7.5.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Ensure-File([string]$Path, [string]$Content) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    if (-not (Test-Path $Path)) {
        $Content | Out-File -FilePath $Path -Encoding UTF8 -Force
        Write-Output ("Created: {0}" -f $Path)
    }
    else {
        Write-Output ("Exists: {0}" -f $Path)
    }
}

Write-Output 'Profile paths:'
$paths = [pscustomobject]@{
    CurrentUserAllHosts    = $PROFILE.CurrentUserAllHosts
    CurrentUserCurrentHost = $PROFILE.CurrentUserCurrentHost
    AllUsersAllHosts       = $PROFILE.AllUsersAllHosts
    AllUsersCurrentHost    = $PROFILE.AllUsersCurrentHost
}
$paths | Format-List | Out-String | Write-Output

$header = @(
    '# PowerShell 7 profile'
    '# Managed by scripts/tools/ensure-profiles.ps1'
    "# Generated: $(Get-Date -Format o)"
    ''
    '# Keep minimal to avoid perf/compat issues'
    "Set-PSReadLineOption -EditMode Windows -ErrorAction SilentlyContinue"
) -join "`n"

Ensure-File -Path $PROFILE.CurrentUserAllHosts -Content $header
Ensure-File -Path $PROFILE.CurrentUserCurrentHost -Content $header
