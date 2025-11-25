param(
    [string]$Path = 'C:\Users\biges\Desktop\Wiley-Widget\src\WileyWidget.WinUI\obj\Debug\net10.0-windows10.0.26100.0\input.json'
)
if (Test-Path -LiteralPath $Path) {
    try {
        $j = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
        if ($null -ne $j.XamlPages) {
            Write-Host "XamlPages count: $($j.XamlPages.Count)"
            $j.XamlPages | ForEach-Object { Write-Host $_.RelativePath }
        } else {
            Write-Host 'No XamlPages found in input.json'
        }
    } catch {
        Write-Host 'Failed to parse input.json:' $_.Exception.Message
    }
} else {
    Write-Host "input.json not found at $Path"
}
