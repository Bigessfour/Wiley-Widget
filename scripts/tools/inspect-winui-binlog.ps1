Param(
    [string]$Binlog = "WinUI.binlog"
)
if (-not (Test-Path $Binlog)) { Write-Host "Binlog not found: $Binlog"; exit 2 }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($Binlog)
Write-Host ("Entries in {0}`n" -f $Binlog)
$entries = $zip.Entries | Sort-Object Length -Descending
foreach ($e in $entries) {
    Write-Host ("{0}  ({1} bytes)" -f $e.FullName, $e.Length)
}

# Look for entries likely to contain errors
$patterns = @('MSB3073','XamlCompiler','Xaml','error','Exception','StackTrace')
foreach ($e in $entries) {
    if ($e.Length -gt 0 -and ($e.FullName -match 'log|text|xml|entry|stdout|stderr|message' -or $e.FullName -match 'Errors' -or $e.FullName -match 'output')) {
        try {
            $sr = [System.IO.StreamReader]::new($e.Open())
            $content = $sr.ReadToEnd()
            foreach ($p in $patterns) {
                if ($content -match $p) {
                    Write-Host "\n=== MATCH in entry: $($e.FullName) for pattern: $p ===\n"
                    $matches = ($content -split "\r?\n") | Select-String -Pattern $p -AllMatches
                    $matches | ForEach-Object { Write-Host $_.ToString() }
                }
            }
            $sr.Close()
        } catch {
            # ignore binary/unsupported entries
        }
    }
}
$zip.Dispose()
