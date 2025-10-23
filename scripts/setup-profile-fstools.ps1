[CmdletBinding()]
param(
    [string]$WorkspacePath = 'C:\Users\biges\Desktop\Wiley_Widget'
)

$ErrorActionPreference = 'Stop'

$profileAll = $PROFILE.CurrentUserAllHosts
$profileDir = Split-Path $profileAll
if (!(Test-Path $profileDir)) {
    New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
}

# Backup existing profile
if (Test-Path $profileAll) {
    Copy-Item -Path $profileAll -Destination ($profileAll + '.bak') -Force
}

$modulesRoot = Join-Path $WorkspacePath 'scripts\Modules'
$fsToolsPath = Join-Path $modulesRoot 'FsTools'

# Build idempotent profile block
$block = @"
# BEGIN WileyWidget FsTools
# Auto-added to import FsTools from: $fsToolsPath
try {
    if (Test-Path '$modulesRoot') {
        `$paths = `$env:PSModulePath -split ';'
        if (`$paths -notcontains '$modulesRoot') {
            `$env:PSModulePath = '$modulesRoot;' + `$env:PSModulePath
        }
    }
    if (Get-Module -ListAvailable -Name FsTools) {
        Import-Module FsTools -Force -ErrorAction SilentlyContinue
    } elseif (Test-Path '$fsToolsPath\FsTools.psm1') {
        Import-Module '$fsToolsPath\FsTools.psm1' -Force -ErrorAction SilentlyContinue
    }
    # Convenience function to manually reload
    function Reload-FsTools { Import-Module FsTools -Force -ErrorAction SilentlyContinue }
} catch {
    Write-Verbose ('FsTools import failed: ' + `$_.Exception.Message)
}
# END WileyWidget FsTools
"@

# Idempotent replace
$current = ''
if (Test-Path $profileAll) { $current = Get-Content -Path $profileAll -Raw -ErrorAction SilentlyContinue }

$newContent = if ($current) {
    [regex]::Replace($current, '(?s)# BEGIN WileyWidget FsTools.*?# END WileyWidget FsTools\s*', '')
}
else { '' }

$newContent = ($newContent.TrimEnd() + "`r`n`r`n" + $block + "`r`n")

Set-Content -Path $profileAll -Value $newContent -Encoding UTF8

Write-Host "Updated profile: $profileAll"
Write-Host "ModulesRoot: $modulesRoot"
Write-Host "FsTools path: $fsToolsPath"
