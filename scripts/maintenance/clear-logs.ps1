# Clear all log files from logs and log folders
Write-Host "Clearing log files..."

$logFolders = @(
    (Join-Path $PSScriptRoot "../../logs"),
    (Join-Path $PSScriptRoot "../../log")
)

foreach ($folder in $logFolders) {
    $resolvedPath = Resolve-Path -Path $folder -ErrorAction SilentlyContinue
    if ($resolvedPath) {
        try {
            $fileCount = @(Get-ChildItem -Path $resolvedPath -File -ErrorAction SilentlyContinue).Count
            Get-ChildItem -Path $resolvedPath -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
            Write-Host "✓ Cleared $fileCount log file(s) in: $resolvedPath"
        }
        catch {
            Write-Host "✗ Error clearing logs in: $resolvedPath - $_"
        }
    }
    else {
        Write-Host "⚠ Log folder not found: $folder"
    }
}

Write-Host "Log cleanup complete."
