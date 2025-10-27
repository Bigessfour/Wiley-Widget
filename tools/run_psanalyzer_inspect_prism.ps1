# Runs PSScriptAnalyzer on inspect_prism.ps1 and prints results
param()

try {
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Write-Output 'PSScriptAnalyzer not found; installing to CurrentUser scope (may require network)'
        Install-Module -Name PSScriptAnalyzer -Force -Scope CurrentUser -AllowClobber
    }

    Import-Module PSScriptAnalyzer -Force

    $scriptPath = Join-Path $PSScriptRoot 'inspect_prism.ps1'
    if (-not (Test-Path $scriptPath)) {
        Write-Error "inspect_prism.ps1 not found at $scriptPath"
        exit 2
    }

    $issues = Invoke-ScriptAnalyzer -Path $scriptPath -Severity Error,Warning
    if ($issues) {
        $issues | Format-List
    } else {
        Write-Output 'No findings'
    }
} catch {
    Write-Error "Failed to run PSScriptAnalyzer: $($_.Exception.Message)"
    exit 1
}
