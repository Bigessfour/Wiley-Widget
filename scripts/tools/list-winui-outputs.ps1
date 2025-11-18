Set-Location 'C:\Users\biges\Desktop\Wiley-Widget'
Write-Host "Listing WinUI obj win-x64 directory (if present)..."
$dir = 'src\WileyWidget.WinUI\obj\x64\Debug\net9.0-windows10.0.26100.0\win-x64'
if (Test-Path $dir) {
    Get-ChildItem -Path $dir -Recurse -File | ForEach-Object {
        $kb = [math]::Round($_.Length / 1KB, 2)
        Write-Host ("{0} - {1} KB" -f $_.FullName, $kb)
    }
} else {
    Write-Host "Directory not found: $dir"
}

Write-Host "\nChecking root for logs..."
foreach ($f in 'WinUI.binlog','xaml_err.txt','xaml_out.txt','winui_diag.txt') {
    if (Test-Path $f) {
        $s = (Get-Item $f).Length
        Write-Host "$f exists - size: $s"
    } else {
        Write-Host "$f not found"
    }
}
