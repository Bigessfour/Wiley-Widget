# Creates a COPY_ONLY backup of WileyWidgetDev into C:\tmp
Param()
$ts = Get-Date -Format yyyyMMdd_HHmmss
$dir = 'C:\tmp'
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$path = Join-Path $dir "WileyWidgetDev_backup_$ts.bak"
$sql = "BACKUP DATABASE [WileyWidgetDev] TO DISK='" + $path + "' WITH COPY_ONLY;"
Write-Output "Executing: $sql"
Write-Output "Attempting sqlcmd with multiple server name variants..."
$servers = @('localhost\\SQLEXPRESS', '.\\SQLEXPRESS', '(local)\\SQLEXPRESS', 'localhost')
$success = $false
foreach ($sv in $servers) {
    Write-Output "Trying sqlcmd against server: $sv"
    & sqlcmd -S $sv -Q $sql
    if ($LASTEXITCODE -eq 0) {
        Write-Output "sqlcmd succeeded against $sv"
        $success = $true
        break
    }
    else {
        Write-Output "sqlcmd failed against $sv (exit $LASTEXITCODE)"
    }
}
if (-not $success) {
    Write-Output "sqlcmd attempts failed; falling back to ADO.NET backup method..."
    try {
        $connectionString = "Server=localhost\\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True;"
        $conn = New-Object System.Data.SqlClient.SqlConnection $connectionString
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 0
        $cmd.CommandText = $sql
        $cmd.ExecuteNonQuery() | Out-Null
        $conn.Close()
        Write-Output "BACKUP_DONE:$path (via ADO.NET)"
    }
    catch {
        Write-Error "ADO.NET backup failed: $_"
        exit 1
    }
}
Write-Output "BACKUP_DONE:$path"
