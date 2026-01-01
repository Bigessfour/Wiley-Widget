Import-Module PSScriptAnalyzer -ErrorAction Stop
$results = Invoke-ScriptAnalyzer -Path 'scripts/tools/dump-ui-tree.ps1' -ErrorAction Stop
if ($null -eq $results -or $results.Count -eq 0) {
    Write-Host 'No analyzer findings.'
    exit 0
}
else {
    $results | Select-Object Severity,RuleName,Line,Message | Format-Table -AutoSize
    exit 1
}
