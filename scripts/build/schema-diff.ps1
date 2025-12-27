# Schema diff: compare EF ModelSnapshot to live DB (table + column level)
$snapshot = 'src\WileyWidget.Data\Migrations\AppDbContextModelSnapshot.cs'
if (-not (Test-Path $snapshot)) { Write-Error 'Model snapshot not found at ' + $snapshot; exit 2 }
$lines = Get-Content -Path $snapshot
$tables = @{}

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match 'b\.ToTable\("(?<t>[^"]+)"') {
        $t = $matches['t']
        # find start of entity block
        $start = $i
        while ($start -ge 0 -and $lines[$start] -notmatch 'modelBuilder\.Entity') { $start-- }
        if ($start -lt 0) { $start = 0 }
        $cols = @()
        for ($j = $start; $j -le $i; $j++) {
            if ($lines[$j] -match 'b\.Property\b.*\("(?<c>[^"]+)"') { $cols += $matches['c'] }
        }
        $tables[$t] = $cols | Sort-Object -Unique
    }
}

Write-Host "Found model tables: $($tables.Keys.Count)"
foreach ($k in $tables.Keys | Sort-Object) { Write-Host "- $k ($($tables[$k].Count) cols)" }

# Query DB columns
$cs = 'Server=localhost\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;'
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,IS_NULLABLE,COALESCE(CHARACTER_MAXIMUM_LENGTH,0) AS MaxLen FROM INFORMATION_SCHEMA.COLUMNS'
    $reader = $cmd.ExecuteReader()
    $dbtables = @{}
    while ($reader.Read()) {
        $tn = $reader['TABLE_NAME']
        if (-not $dbtables.ContainsKey($tn)) { $dbtables[$tn] = @() }
        $dbtables[$tn] += @{ Column = $reader['COLUMN_NAME']; DataType = $reader['DATA_TYPE']; IsNullable = $reader['IS_NULLABLE']; MaxLen = $reader['MaxLen'] }
    }
    $reader.Close()
}
catch {
    Write-Error $_
    exit 3
}
finally { $conn.Close() }

Write-Host "DB tables: $($dbtables.Keys.Count)"
foreach ($k in $dbtables.Keys | Sort-Object) { Write-Host "- $k ($($dbtables[$k].Count) cols)" }

# Compare
$modelOnly = $tables.Keys | Where-Object { -not $dbtables.ContainsKey($_) } | Sort-Object
$dbOnly = $dbtables.Keys | Where-Object { -not $tables.ContainsKey($_) } | Sort-Object
if ($modelOnly.Count -gt 0) { Write-Host "`nTables in model but missing in DB:"; $modelOnly | ForEach-Object { Write-Host " - $_" } } else { Write-Host "`nNo model-only tables" }
if ($dbOnly.Count -gt 0) { Write-Host "`nTables in DB but missing in model:"; $dbOnly | ForEach-Object { Write-Host " - $_" } } else { Write-Host "`nNo db-only tables" }

Write-Host "`nColumn diffs (model vs DB):"
$common = $tables.Keys | Where-Object { $dbtables.ContainsKey($_) } | Sort-Object
foreach ($t in $common) {
    $modelCols = $tables[$t]
    $dbCols = $dbtables[$t] | ForEach-Object { $_.Column }
    $modelOnlyCols = $modelCols | Where-Object { $dbCols -notcontains $_ }
    $dbOnlyCols = $dbCols | Where-Object { $modelCols -notcontains $_ }
    if ($modelOnlyCols.Count -gt 0 -or $dbOnlyCols.Count -gt 0) {
        Write-Host "`nTable: $t"
        if ($modelOnlyCols.Count -gt 0) { Write-Host "  Model-only columns: $($modelOnlyCols -join ', ')" }
        if ($dbOnlyCols.Count -gt 0) { Write-Host "  DB-only columns: $($dbOnlyCols -join ', ')" }
    }
}

exit 0
