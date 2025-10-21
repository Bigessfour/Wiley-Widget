$scriptPath = $args[0]
if (-not $scriptPath) { Write-Error 'Usage: run-pssa.ps1 <scriptPath>'; exit 2 }
try {
    $results = Invoke-ScriptAnalyzer -Path $scriptPath -Severity Warning, Error
    if (-not $results -or $results.Count -eq 0) {
        Write-Output 'PSScriptAnalyzer: No warnings or errors found.'
        exit 0
    }
    $results | Select-Object RuleName, Severity, ScriptName, Line, Message | Format-Table -AutoSize
    exit 0
}
catch {
    Write-Error "PSScriptAnalyzer invocation failed: $($_.Exception.Message)"
    exit 3
}
