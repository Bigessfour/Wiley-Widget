# find_connection.ps1 - probe multiple connection strings and print the first that succeeds (masked)
param(
    [string]$Server = '.\\SQLEXPRESS',
    [string]$Database = 'WileyWidgetDev'
)
Add-Type -AssemblyName System.Data
$candidates = @()
$candidates += "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"

# detect instance name if present
$inst = $null
if ($Server -like '*\\*') {
    $parts = $Server -split '\\'
    $inst = $parts[-1]
}
if ($inst) {
    $np = "Server=np:\\\\.\\\\pipe\\MSSQL$inst\\sql\\query;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
    $candidates += $np
}

$commonPorts = @(1433, 14333)
foreach ($p in $commonPorts) {
    $candidates += "Server=tcp:localhost,$p;Database=$Database;Integrated Security=True;TrustServerCertificate=True;"
}

function Mask([string]$cs) {
    return ($cs -replace 'Password=[^;]*', 'Password=*****' -replace 'User Id=[^;]*', 'User Id=*****')
}

$success = $null
foreach ($cs in $candidates) {
    Write-Output "Trying: $(Mask $cs)"
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
        $conn.Open()
        if ($conn.State -eq 'Open') {
            $conn.Close()
            $success = $cs
            break
        }
    }
    catch {
        Write-Output "Fail: $($_.Exception.GetType().FullName) - $($_.Exception.Message)"
    }
    finally {
        if ($conn -and $conn.State -eq 'Open') { $conn.Close() }
    }
}

if ($success) {
    Write-Output "SUCCESS: $(Mask $success)"
    exit 0
}
else {
    Write-Output "NO SUCCESS: no candidate connection string opened"
    exit 2
}
