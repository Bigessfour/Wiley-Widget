<#
.SYNOPSIS
    Syncfusion license profile initialization script.

.DESCRIPTION
    This script initializes the Syncfusion license for the current PowerShell session.
    It can optionally persist the license to environment files.

.PARAMETER Persist
    If specified, persists the license key to the .env file.
#>
[CmdletBinding()]
param(
    [switch]$Persist
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ensure = Join-Path $scriptRoot 'ensure-syncfusion-license.ps1'

if (-not (Test-Path $ensure)) {
    Write-Warning "ensure-syncfusion-license.ps1 not found; skipping Syncfusion license init."
    return
}

# Clean any previously malformed multi-line entry (retain header comments)
$envFile = Join-Path (Split-Path -Parent $scriptRoot) '.env'
if (Test-Path $envFile) {
    $lines = Get-Content $envFile
    $fixed = @()
    $inKeyBlock = $false

    foreach ($l in $lines) {
        if ($l -match '^SYNCFUSION_LICENSE_KEY=') {
            if ($l -match '=#') {
                $fixed += 'SYNCFUSION_LICENSE_KEY=YOUR_SYNCFUSION_LICENSE_KEY_HERE'
            } else {
                $fixed += $l
            }
            $inKeyBlock = $true
            continue
        }

        if ($inKeyBlock) {
            if ([string]::IsNullOrWhiteSpace($l) -or ($l -match '^[A-Z0-9_]+=')) {
                $inKeyBlock = $false
            } else {
                continue
            }
        }

        if (-not $inKeyBlock) {
            $fixed += $l
        }
    }

    if ($null -ne $fixed) {
        $fixed | Set-Content -Encoding UTF8 $envFile
    }
}

$argsList = @('-PreferMachine', '-SyncMachineToEnv', '-Quiet')
if ($Persist) {
    $argsList += '-PersistToEnvFile'
}

& pwsh $ensure @argsList | Out-Null

$k = [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Process')
if ($k) {
    Write-Information "Syncfusion license loaded (len=$($k.Length))" -InformationAction Continue
} else {
    Write-Warning 'Syncfusion license not loaded.'
}
