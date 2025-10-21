# diagnostics_sqlserver.ps1
# Run a set of local diagnostics for SQL Server Express (.\SQLEXPRESS)
# Outputs services, processes, netstat listening entries, sqlcmd availability and DB list via ADO.NET

Write-Output "== Diagnostics: $(Get-Date -Format o) =="

Write-Output "\n-- Services --"
try {
    Get-Service -Name 'MSSQL$SQLEXPRESS', 'SQLBrowser' -ErrorAction SilentlyContinue | Select-Object Name, DisplayName, Status | Format-Table -AutoSize
}
catch {
    Write-Error "Failed to list services: $_"
}

Write-Output "\n-- sqlservr processes --"
try {
    $sqlProcs = Get-Process -Name sqlservr -ErrorAction SilentlyContinue
    if (-not $sqlProcs) {
        Write-Output "No sqlservr process found"
    }
    else {
        $sqlProcs | Select-Object Id, ProcessName, StartTime | Format-Table -AutoSize
    }
}
catch {
    Write-Error "Failed to list processes: $_"
}

Write-Output "\n-- netstat (listening entries for sqlservr pids) --"
try {
    if ($sqlProcs) {
        $pids = $sqlProcs | Select-Object -ExpandProperty Id
        $pattern = ($pids -join '|')
        Write-Output "Filtering netstat for PIDs: $pattern"
        netstat -ano | Select-String "LISTENING" | Select-String $pattern | ForEach-Object { $_.ToString() }
    }
    else {
        Write-Output "Skipping netstat filter because no sqlservr processes were found"
    }
}
catch {
    Write-Error "Failed to run netstat: $_"
}

Write-Output "\n-- sqlcmd check --"
try {
    if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
        Write-Output "sqlcmd found. Listing databases via sqlcmd:"
        sqlcmd -S ".\\SQLEXPRESS" -E -Q "SELECT name FROM sys.databases ORDER BY name;"
    }
    else {
        Write-Output "sqlcmd not installed or not on PATH"
    }
}
catch {
    Write-Error "sqlcmd invocation failed: $_"
}

Write-Output "\n-- ADO.NET check (SqlClient) --"
try {
    Add-Type -AssemblyName System.Data
    $connStr = 'Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True;'
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT name FROM sys.databases ORDER BY name;'
    $r = $cmd.ExecuteReader()
    while ($r.Read()) { Write-Output $r.GetString(0) }
    $conn.Close()
}
catch {
    Write-Error "ADO.NET connection/query failed: $_"
}

Write-Output "\n== Diagnostics complete =="
