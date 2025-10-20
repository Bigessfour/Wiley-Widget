#requires -Version 7.0
<#!
.SYNOPSIS
Performs diagnostics for Cloudflare tunnel connectivity.

.DESCRIPTION
Checks for the presence of a Cloudflare cert.pem, tests DNS resolution for a provided hostname,
verifies that a local service port is listening, and logs results to a file and pipeline-safe output.

The script is safe for CI: it uses Write-Output/Write-Information (no Write-Host) and returns a
structured object summarizing results. A transcript and detailed log are written to the logs folder.

.PARAMETER Hostname
The fully qualified domain name to resolve (e.g., tunnel.example.com).

.PARAMETER Port
The local TCP port to test for connectivity (e.g., 8080).

.PARAMETER CertPath
Path to Cloudflare cert.pem. Defaults to the typical user profile location.

.PARAMETER LogDirectory
Directory where logs will be stored. Defaults to the repository logs/cloudflare directory.

.PARAMETER Quiet
Suppress informational logging (but not errors). Useful for very quiet CI logs.

.EXAMPLE
pwsh -File tools/cloudflare/diagnose-cloudflare-tunnel.ps1 -Hostname "my-tunnel.example.com" -Port 8080

.EXAMPLE
./diagnose-cloudflare-tunnel.ps1 -Hostname dev.example.com -Port 5000 -Verbose

.NOTES
PowerShell 7+ is recommended. Designed for Windows runners but works cross-platform for DNS checks.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$Hostname = $env:CLOUDFLARE_TUNNEL_HOST,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1,65535)]
    [int]$Port = [int]([System.Environment]::GetEnvironmentVariable('LOCAL_SERVICE_PORT') ?? '0'),

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$CertPath = (Join-Path $env:USERPROFILE ".cloudflared/cert.pem"),

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string]$LogDirectory
,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    [CmdletBinding()]
    [OutputType([string])]
    param()
    # Script lives under tools/cloudflare; repo root is two levels up
    $root = Resolve-Path (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '..')
    return $root.Path
}

function New-LogFilePath {
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)] [string]$BaseDirectory
    )
    $lf = Join-Path $BaseDirectory ("diagnostics-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".log")
    if (-not (Test-Path -LiteralPath $BaseDirectory)) {
        if ($PSCmdlet.ShouldProcess($BaseDirectory, "Create log directory")) {
            New-Item -ItemType Directory -Force -Path $BaseDirectory | Out-Null
        }
    }
    return $lf
}

function Write-CfLog {
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)] [string]$Message,
        [Parameter(Mandatory)] [ValidateSet('INFO','WARN','ERROR','DEBUG')][string]$Level,
        [Parameter(Mandatory)] [string]$LogPath
    )
    $ts = (Get-Date).ToString('o')
    $line = "[$ts] [$Level] $Message"
    Add-Content -Path $LogPath -Value $line
    switch ($Level) {
        'INFO' { if (-not $Quiet) { Write-Information $Message -InformationAction Continue } }
        'WARN' { Write-Warning $Message }
        'ERROR' { Write-Error $Message }
        'DEBUG' { Write-Verbose $Message }
    }
}

function Test-CertPath {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$LogPath
    )
    $exists = Test-Path -LiteralPath $Path -PathType Leaf
    if ($exists) {
        Write-CfLog -Message "Found cert file: $Path" -Level INFO -LogPath $LogPath
    } else {
        Write-CfLog -Message "cert.pem not found at: $Path" -Level WARN -LogPath $LogPath
    }
    [PSCustomObject]@{ CertExists = $exists; CertPath = $Path }
}

function Invoke-NslookupFallback {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$LogPath
    )
    $ips = @()
    $ok = $false
    try {
        $nsResult = nslookup $Name 2>$null
        $addressMatches = ($nsResult | Select-String -Pattern 'Address:\s*([0-9]{1,3}(?:\.[0-9]{1,3}){3}|([a-fA-F0-9:]+))' -AllMatches)
        $foundMatches = @()
        if ($addressMatches) {
            foreach ($matchObj in $addressMatches) {
                foreach ($m in $matchObj.Matches) {
                    $foundMatches += $m.Groups[1].Value
                }
            }
        }
        $ips = $foundMatches | Select-Object -Unique
        $ok = ($ips.Count -gt 0)
        if ($ok) {
            Write-CfLog -Message "nslookup resolved $Name to: $($ips -join ', ')" -Level INFO -LogPath $LogPath
        } else {
            Write-CfLog -Message "nslookup found no A/AAAA records for $Name" -Level WARN -LogPath $LogPath
        }
    } catch {
        Write-CfLog -Message "nslookup failed for $Name: $($_.Exception.Message)" -Level ERROR -LogPath $LogPath
        $ok = $false
    }
    return @{ DnsResolved = [bool]$ok; IPAddresses = $ips }
}

function Test-DnsResolution {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string]$Name,
        [Parameter(Mandatory)] [string]$LogPath
    )
    $ips = @()
    $ok = $false
    if ([string]::IsNullOrWhiteSpace($Name)) {
        Write-CfLog -Message "No hostname provided; skipping DNS resolution." -Level WARN -LogPath $LogPath
        return [PSCustomObject]@{ DnsResolved = $false; Hostname = $Name; IPAddresses = @() }
    }
    try {
        $res = Resolve-DnsName -Name $Name -ErrorAction Stop
        $ips = $res | Where-Object { $_.IPAddress } | Select-Object -ExpandProperty IPAddress -Unique
        $ok = ($ips.Count -gt 0)
        if ($ok) {
            Write-CfLog -Message "Resolved $Name to: $($ips -join ', ')" -Level INFO -LogPath $LogPath
        } else {
            Write-CfLog -Message "Resolve-DnsName returned no IPs for $Name" -Level WARN -LogPath $LogPath
        }
    } catch {
        Write-CfLog -Message "Resolve-DnsName failed for $Name: $($_.Exception.Message)" -Level WARN -LogPath $LogPath
        # Fallback to nslookup if available
        if (Get-Command nslookup -ErrorAction SilentlyContinue) {
            $nsResult = Invoke-NslookupFallback -Name $Name -LogPath $LogPath
            $ips = $nsResult.IPAddresses
            $ok = $nsResult.DnsResolved
        } else {
            Write-CfLog -Message "nslookup is not available on this system; skipping fallback DNS resolution." -Level WARN -LogPath $LogPath
            $ok = $false
        }
    }
    [PSCustomObject]@{ DnsResolved = [bool]$ok; Hostname = $Name; IPAddresses = $ips }
}
function Test-LocalPort {
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)] [ValidateRange(1,65535)] [int]$TcpPort,
        [Parameter(Mandatory)] [string]$LogPath
    )
    if ($TcpPort -le 0) {
        Write-CfLog -Message "No port provided; skipping local port test." -Level WARN -LogPath $LogPath
        return [PSCustomObject]@{ PortOpen = $false; Port = $TcpPort; Method = 'Skipped' }
    }
    $open = $false
    $method = 'Test-NetConnection'
    try {
        $tnc = Test-NetConnection -ComputerName '127.0.0.1' -Port $TcpPort -WarningAction SilentlyContinue -InformationLevel Detailed
        if ($tnc -and $tnc.TcpTestSucceeded) {
            $open = $true
            Write-CfLog -Message "Port $TcpPort is open on localhost (Test-NetConnection)." -Level INFO -LogPath $LogPath
        } else {
            Write-CfLog -Message "Port $TcpPort not open via Test-NetConnection; trying TcpClient." -Level WARN -LogPath $LogPath
            $method = 'TcpClient'
            $client = [System.Net.Sockets.TcpClient]::new()
            try {
                $iar = $client.BeginConnect('127.0.0.1', $TcpPort, $null, $null)
                $connected = $iar.AsyncWaitHandle.WaitOne([TimeSpan]::FromSeconds(2))
                if ($connected -and $client.Connected) { $open = $true }
            } finally {
                $client.Close()
                $client.Dispose()
            }
        }
    } catch {
        Write-CfLog -Message "Local port test failed: $($_.Exception.Message)" -Level ERROR -LogPath $LogPath
        $open = $false
    }
    if ($open) {
        Write-CfLog -Message "Port $TcpPort appears to be accepting connections." -Level INFO -LogPath $LogPath
    } else {
        Write-CfLog -Message "Port $TcpPort does not appear to be open on localhost." -Level WARN -LogPath $LogPath
    }
    [PSCustomObject]@{ PortOpen = $open; Port = $TcpPort; Method = $method }
}

# Establish log directory
if (-not $PSBoundParameters.ContainsKey('LogDirectory') -or [string]::IsNullOrWhiteSpace($LogDirectory)) {
    $repoRoot = Get-RepoRoot
    $LogDirectory = Join-Path $repoRoot 'logs/cloudflare'
}
if (-not (Test-Path -LiteralPath $LogDirectory)) { New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null }
$logFile = New-LogFilePath -BaseDirectory $LogDirectory

# Start transcript for detailed capture (best-effort)
try {
    Start-Transcript -Path (Join-Path $LogDirectory "transcript-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt") -ErrorAction Stop | Out-Null
} catch {
    Write-Verbose "Start-Transcript failed: $($_.Exception.Message)"
}

# Environment header
Write-CfLog -Message "PowerShell: $($PSVersionTable.PSVersion) | OS: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)" -Level INFO -LogPath $logFile
Write-CfLog -Message "WorkingDir: $(Get-Location) | ScriptRoot: $PSScriptRoot" -Level INFO -LogPath $logFile

# Run checks
$certResult = Test-CertPath -Path $CertPath -LogPath $logFile
$dnsResult  = Test-DnsResolution -Name $Hostname -LogPath $logFile
$portResult = Test-LocalPort -TcpPort $Port -LogPath $logFile

$result = [PSCustomObject]@{
    Timestamp   = Get-Date
    Hostname    = $dnsResult.Hostname
    DnsResolved = [bool]$dnsResult.DnsResolved
    IPAddresses = $dnsResult.IPAddresses
    Port        = $portResult.Port
    PortOpen    = [bool]$portResult.PortOpen
    CertPath    = $certResult.CertPath
    CertExists  = [bool]$certResult.CertExists
    LogPath     = $logFile
}

# Summary
Write-CfLog -Message (
    "Summary => CertExists=$($result.CertExists); DnsResolved=$($result.DnsResolved)" +
    "; PortOpen=$($result.PortOpen); Log=$($result.LogPath)"
) -Level INFO -LogPath $logFile

# Stop transcript
try {
    Stop-Transcript | Out-Null
} catch {
    Write-Verbose "Stop-Transcript failed: $($_.Exception.Message)"
}

# Emit structured result for CI consumers
$result | Write-Output
