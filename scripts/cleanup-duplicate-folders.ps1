param(
    [string]$Root = "C:\\Users\\biges\\Desktop\\Wiley_Widget"
)

$paths = @(
    'src\WileyWidget.Data\Interceptors',
    'src\WileyWidget.Data',
    'WileyWidget.Data\WileyWidget.Data\WileyWidget.Data',
    'WileyWidget.Data\WileyWidget.Data'
)

foreach ($p in $paths) {
    $full = Join-Path $Root $p
    if (Test-Path $full) {
        $count = (Get-ChildItem -Path $full -Recurse -Force | Measure-Object).Count
        if ($count -eq 0) {
            Remove-Item -Path $full -Force -Recurse -ErrorAction SilentlyContinue
            Write-Output "Removed empty: $p"
        }
        else {
            Write-Output "Not empty, skipped: $p ($count items)"
        }
    }
    else {
        Write-Output "Not found: $p"
    }
}
