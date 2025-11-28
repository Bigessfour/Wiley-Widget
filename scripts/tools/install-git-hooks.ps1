<#
.SYNOPSIS
    Install repository git hooks for local development (installs pre-commit hook to lint PowerShell profiles)
.DESCRIPTION
    Writes a simple pre-commit hook into .git/hooks/pre-commit calling the lint script.
    Developers should review the script before running; this is intentionally interactive and non-destructive.
#>

param(
    [switch]$Force
)

$hookPath = Join-Path (Get-Location) '.git/hooks/pre-commit'
$script = Join-Path (Get-Location) 'scripts/tools/lint-powershell-profiles.ps1'

if (-not (Test-Path $script)) {
    Write-Error "Lint script not found: $script"
    exit 1
}

$hookContent = @"
#!/usr/bin/env pwsh
# Pre-commit: run workspace PS profile lint
if (Get-Command Invoke-ScriptAnalyzer -ErrorAction SilentlyContinue) {
    & "$script" -FailOnWarning
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Pre-commit: PowerShell lint failed. Fix issues before committing.'
        exit 1
    }
} else {
    Write-Host 'PSScriptAnalyzer not installed locally; skipping PowerShell lint. Install with: Install-Module -Name PSScriptAnalyzer -Scope CurrentUser'
}
exit 0
"@

if ((Test-Path $hookPath) -and -not $Force) {
    Write-Warning "A pre-commit hook already exists at $hookPath. Use -Force to overwrite."
    exit 0
}

try {
    $dir = Split-Path $hookPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $hookContent | Out-File -FilePath $hookPath -Encoding UTF8 -Force
    # Make sure it's executable (on Windows this is advisory for Git)
    if ($IsWindows) { icacls $hookPath /grant "$(whoami):F" | Out-Null }
    Write-Host "Installed pre-commit hook at: $hookPath" -ForegroundColor Green
} catch {
    Write-Error "Failed to install pre-commit hook: $_"
    exit 1
}

exit 0
