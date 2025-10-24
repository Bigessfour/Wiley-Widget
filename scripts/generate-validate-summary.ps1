# Helper to group Validate-ViewModels report by ViewModel
Set-StrictMode -Version Latest
Set-Location -Path $PSScriptRoot\..\

$inputPath = Join-Path -Path (Get-Location) -ChildPath "scripts\Validate-ViewModels-report.json"
$outputPath = Join-Path -Path (Get-Location) -ChildPath "scripts\Validate-ViewModels-summary.json"

if (-not (Test-Path $inputPath)) {
    Write-Error "Input file not found: $inputPath"
    exit 1
}

# Read JSON (raw) and convert
$json = Get-Content -Raw -Encoding UTF8 -Path $inputPath
$data = $json | ConvertFrom-Json

# Group by ViewModel
$grouped = $data | Group-Object -Property ViewModel | ForEach-Object {
    $errors = $_.Group | Where-Object { $_.Severity -eq 'Error' } | Select-Object -ExpandProperty Message
    $warnings = $_.Group | Where-Object { $_.Severity -eq 'Warning' } | Select-Object -ExpandProperty Message
    [PSCustomObject]@{
        ViewModel = $_.Name
        Count     = $_.Count
        Errors    = $errors
        Warnings  = $warnings
        Files     = ($_.Group | Select-Object -ExpandProperty File | Sort-Object -Unique)
    }
} | Sort-Object -Property Count -Descending

# Write summary
$grouped | ConvertTo-Json -Depth 6 | Out-File -FilePath $outputPath -Encoding UTF8
Write-Output "WROTE: $outputPath"

# Also print top 6 to console for quick inspection
$grouped | Select-Object -First 6 | Format-Table ViewModel, Count -AutoSize
