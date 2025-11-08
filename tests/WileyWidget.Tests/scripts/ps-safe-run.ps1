<#
.SYNOPSIS
  Safe-run wrapper: analyze a script or folder with PSScriptAnalyzer, fail on warnings/errors,
  and optionally run the script (supports DryRun).

.PARAMETER ScriptPath
  Path to a script file or folder to analyze/run. If omitted, defaults to the repository scripts folder.

.PARAMETER DryRun
  When set, the wrapper will analyze and report results but will not execute the target script.

.EXAMPLE
  .\ps-safe-run.ps1 -ScriptPath .\scripts -DryRun

#>
param(
    [string]$ScriptPath = "$PSScriptRoot",
    [switch]$DryRun,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
try {
    # Ensure we use the expected PowerShell version
    if ($PSVersionTable.PSVersion -lt [version]'7.5.0') {
        Write-Error "PowerShell 7.5.x is required. Current: $($PSVersionTable.PSVersion)"
        exit 2
    }

    # Locate PSScriptAnalyzer settings path from workspace if available
    $settingsPath = Join-Path -Path (Split-Path -Parent $PSScriptRoot) -ChildPath ".vscode\PSScriptAnalyzerSettings.psd1"
    if (-not (Test-Path $settingsPath)) {
        Write-Verbose "No .vscode/PSScriptAnalyzerSettings.psd1 found; using defaults"
        $settingsPath = $null
    }

    # Import PSScriptAnalyzer if possible
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Write-Warning "PSScriptAnalyzer module not found. Install with: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser -Force"
        exit 3
    }
    Import-Module PSScriptAnalyzer -ErrorAction Stop

    Write-Information "Running Script Analyzer on: $ScriptPath"

    $invokeParams = @{ Path = $ScriptPath; Recurse = $true }
    if ($settingsPath) {
        # Support different PSScriptAnalyzer versions: some versions accept -SettingsPath (string),
        # others accept -Settings (hashtable). Detect which parameter exists and pass accordingly.
        $isaCmd = Get-Command Invoke-ScriptAnalyzer -ErrorAction SilentlyContinue
        if ($isaCmd -and $isaCmd.Parameters.ContainsKey('SettingsPath')) {
            $invokeParams['SettingsPath'] = $settingsPath
        }
        elseif ($isaCmd -and $isaCmd.Parameters.ContainsKey('Settings')) {
            try {
                $settingsObj = Import-PowerShellDataFile -Path $settingsPath -ErrorAction Stop
                $invokeParams['Settings'] = $settingsObj
            }
            catch {
                Write-Verbose "Failed to import settings file as data file: $_. Falling back to not passing settings."
            }
        }
    }

    $results = Invoke-ScriptAnalyzer @invokeParams -ErrorAction SilentlyContinue

    if ($null -eq $results -or $results.Count -eq 0) {
        Write-Output "No analyzer findings."
    }
    else {
        # Print findings grouped by severity
        $errors = $results | Where-Object { $_.Severity -in 'Error', 'Warning' }
        if ($errors.Count -gt 0) {
            Write-Error "Analyzer found issues (warnings/errors):"
            $errors | Select-Object Severity, RuleName, Line, Message, ScriptName | Format-Table -AutoSize
            # Treat warnings/errors as failure for this workspace
            exit 4
        }
        else {
            Write-Warning "Analyzer findings present but below severity threshold."
        }
    }

    if ($DryRun) {
        Write-Information "Dry-run requested; skipping execution phase."
        exit 0
    }

    # If ScriptPath is a file, run it; if a folder, no automatic run (avoid accidental execution)
    if (Test-Path $ScriptPath -PathType Leaf) {
        Write-Information "Executing: $ScriptPath"
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @RemainingArgs
        exit $LASTEXITCODE
    }
    else {
        Write-Warning "ScriptPath is a folder. No execution performed by default. Use -ScriptPath <file> to run a script."
        exit 0
    }
}
catch {
    Write-Error "ps-safe-run failed: $_"
    exit 10
}
