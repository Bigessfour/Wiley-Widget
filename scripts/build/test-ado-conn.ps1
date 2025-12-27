$cs = 'Server=localhost\\SQLEXPRESS;Database=WileyWidgetDev;Integrated Security=True;TrustServerCertificate=True;'
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT 1'
    $r = $cmd.ExecuteScalar()
    Write-Output "CONN_OK:$r"
}
catch {
    Write-Output "CONN_ERR:$($_.Exception.Message)"
}
finally {
    if ($conn.State -eq 'Open') {
        $conn.Close()
    }
}
