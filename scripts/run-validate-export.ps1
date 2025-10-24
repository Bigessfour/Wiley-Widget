Set-Location -Path 'c:\Users\biges\Desktop\Wiley_Widget'

# Dot-source the validation script so it runs in this scope and populates $results
. .\scripts\Validate-ViewModels.ps1

if ($null -eq $results) {
    Write-Output 'No $results produced by script'
    exit 1
}

$results | ConvertTo-Json -Depth 6 | Out-File -FilePath .\scripts\Validate-ViewModels-report.json -Encoding utf8
Write-Output 'WROTE: scripts/Validate-ViewModels-report.json'
