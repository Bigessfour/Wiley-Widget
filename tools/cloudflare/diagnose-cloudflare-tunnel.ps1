param(
    [Parameter(Mandatory = $true)]
    [string]$Hostname,

    [Parameter(Mandatory = $false)]
    [int]$Port = 0
)

# Cloudflare Tunnel Diagnostic Script
# Performs basic connectivity tests for Cloudflare tunnel endpoints

$diagnostic = @{
    timestamp      = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    hostname       = $Hostname
    port           = $Port
    tests          = @()
    overall_status = "unknown"
}

function Write-DiagnosticMessage {
    param([string]$Message, [string]$Level = "INFO")
    Write-Verbose "[$Level] $Message"
}

function Test-DNSResolution {
    param([string]$HostName)

    $test = @{
        name    = "DNS Resolution"
        status  = "unknown"
        details = @{}
    }

    try {
        Write-DiagnosticMessage "Testing DNS resolution for $HostName"
        $dnsResult = Resolve-DnsName -Name $HostName -ErrorAction Stop
        $test.status = "success"
        $test.details = @{
            resolved     = $true
            ip_addresses = $dnsResult.IPAddress
            query_type   = $dnsResult.QueryType
        }
        Write-DiagnosticMessage "DNS resolution successful: $($dnsResult.IPAddress -join ', ')"
    }
    catch {
        $test.status = "failed"
        $test.details = @{
            resolved = $false
            error    = $_.Exception.Message
        }
        Write-DiagnosticMessage "DNS resolution failed: $($_.Exception.Message)" "ERROR"
    }

    return $test
}

function Test-PortConnectivity {
    param([string]$HostName, [int]$Port)

    $test = @{
        name    = "Port Connectivity"
        status  = "unknown"
        details = @{}
    }

    if ($Port -eq 0) {
        $test.status = "skipped"
        $test.details = @{
            reason = "No port specified"
        }
        Write-DiagnosticMessage "Skipping port connectivity test - no port specified"
        return $test
    }

    try {
        Write-DiagnosticMessage "Testing connectivity to ${HostName}:$Port"
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $connectTask = $tcpClient.ConnectAsync($HostName, $Port)
        $timeout = 5000  # 5 seconds
        $connected = $connectTask.Wait($timeout)

        if ($connected -and $tcpClient.Connected) {
            $test.status = "success"
            $test.details = @{
                connected  = $true
                timeout_ms = $timeout
            }
            Write-DiagnosticMessage "Port connectivity successful"
        }
        else {
            $test.status = "failed"
            $test.details = @{
                connected  = $false
                timeout_ms = $timeout
                reason     = "Connection timeout or failed"
            }
            Write-DiagnosticMessage "Port connectivity failed - timeout after ${timeout}ms" "ERROR"
        }

        $tcpClient.Close()
    }
    catch {
        $test.status = "failed"
        $test.details = @{
            connected = $false
            error     = $_.Exception.Message
        }
        Write-DiagnosticMessage "Port connectivity test error: $($_.Exception.Message)" "ERROR"
    }

    return $test
}

function Test-CloudflareHeader {
    param([string]$HostName, [int]$Port)

    $test = @{
        name    = "Cloudflare Headers Check"
        status  = "unknown"
        details = @{}
    }

    try {
        Write-DiagnosticMessage "Testing for Cloudflare headers on $HostName"

        $uri = if ($Port -gt 0 -and $Port -ne 80 -and $Port -ne 443) {
            "http://${HostName}:$Port/"
        }
        else {
            "https://${HostName}/"
        }

        $response = Invoke-WebRequest -Uri $uri -Method Head -TimeoutSec 10 -ErrorAction Stop

        $cfHeaders = $response.Headers | Where-Object { $_.Key -like "CF-*" -or $_.Key -like "Server" }

        $test.details = @{
            response_code      = $response.StatusCode
            cloudflare_headers = @{}
        }

        foreach ($header in $cfHeaders) {
            $test.details.cloudflare_headers[$header.Key] = $header.Value
        }

        # Check for common Cloudflare indicators
        $isCloudflare = $false
        if ($test.details.cloudflare_headers.ContainsKey("CF-RAY") -or
            ($test.details.cloudflare_headers.ContainsKey("Server") -and
            $test.details.cloudflare_headers["Server"] -contains "cloudflare")) {
            $isCloudflare = $true
        }

        $test.details.is_cloudflare = $isCloudflare
        $test.status = if ($isCloudflare) { "success" } else { "warning" }

        Write-DiagnosticMessage "Cloudflare headers check completed. Is Cloudflare: $isCloudflare"
    }
    catch {
        $test.status = "failed"
        $test.details = @{
            error         = $_.Exception.Message
            is_cloudflare = $false
        }
        Write-DiagnosticMessage "Cloudflare headers check failed: $($_.Exception.Message)" "ERROR"
    }

    return $test
}

# Run diagnostic tests
Write-DiagnosticMessage "Starting Cloudflare tunnel diagnostics for $Hostname"

$diagnostic.tests += Test-DNSResolution -HostName $Hostname
$diagnostic.tests += Test-PortConnectivity -HostName $Hostname -Port $Port
$diagnostic.tests += Test-CloudflareHeader -HostName $Hostname -Port $Port

# Determine overall status
$failedTests = $diagnostic.tests | Where-Object { $_.status -eq "failed" }
$warningTests = $diagnostic.tests | Where-Object { $_.status -eq "warning" }

if ($failedTests.Count -gt 0) {
    $diagnostic.overall_status = "failed"
}
elseif ($warningTests.Count -gt 0) {
    $diagnostic.overall_status = "warning"
}
else {
    $successTests = $diagnostic.tests | Where-Object { $_.status -eq "success" -or $_.status -eq "skipped" }
    if ($successTests.Count -gt 0) {
        $diagnostic.overall_status = "success"
    }
}

Write-DiagnosticMessage "Diagnostics completed. Overall status: $($diagnostic.overall_status)"

# Return the diagnostic object
return $diagnostic
