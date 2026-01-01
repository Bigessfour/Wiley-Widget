# Quick syntax check for dump-ui-tree.ps1
$content = Get-Content -Raw 'scripts/tools/dump-ui-tree.ps1'
$errors = $null
[System.Management.Automation.Language.Parser]::ParseInput($content, [ref]$null, [ref]$errors)
if ($errors -and $errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_.Message }
    exit 1
}
else {
    Write-Host 'Syntax check passed (no parser errors).'
}
