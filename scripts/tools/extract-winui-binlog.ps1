Param(
    [string]$BinlogPath = "WinUI.binlog"
)

if (-not (Test-Path $BinlogPath)) {
    Write-Host "WinUI.binlog not found: $BinlogPath"
    exit 2
}

Write-Host "Extracting readable fragments from $BinlogPath..."
$bytes = [System.IO.File]::ReadAllBytes($BinlogPath)
try {
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
} catch {
    Write-Host "Failed to decode binlog as UTF8; trying Unicode..."
    $text = [System.Text.Encoding]::Unicode.GetString($bytes)
}
$patterns = @('Xaml','XAML','XamlCompiler','error','Exception','MSB3073','StackTrace')
foreach ($p in $patterns) {
    Write-Host "=== Pattern: $p ==="
    $matches = $text | Select-String -Pattern $p -AllMatches | Select-Object -Unique
    if ($matches) {
        $matches | ForEach-Object { Write-Host $_.ToString() }
    } else {
        Write-Host "(no matches)"
    }
}
