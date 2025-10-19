# Requires: PowerShell 7+
param(
    [switch]$SkipSystemCertRemoval,
    [string]$CloudflaredPath,
    [string]$CertPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Note {
    param([string]$Message)
    Write-Information $Message -InformationAction Continue
}

function Stop-Cloudflared {
    try {
        # Stop Windows service if it exists
        $svc = Get-Service -Name 'cloudflared' -ErrorAction SilentlyContinue
        if ($null -ne $svc -and $svc.Status -ne 'Stopped') {
            Write-Note "Stopping 'cloudflared' service..."
            Stop-Service -Name 'cloudflared' -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Warning "Couldn't stop 'cloudflared' service: $($_.Exception.Message)"
    }

    try {
        # Kill any running cloudflared processes
        $procs = Get-Process -Name 'cloudflared' -ErrorAction SilentlyContinue
        if ($procs) {
            Write-Note "Killing running cloudflared processes..."
            $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Warning "Couldn't kill cloudflared processes: $($_.Exception.Message)"
    }
}

function Remove-CertFile {
    # User profile cert.pem forces re-login when removed
    $userCert = Join-Path $env:USERPROFILE ".cloudflared/cert.pem"
    if (Test-Path $userCert) {
        Write-Note "Removing user cert: $userCert"
        Remove-Item $userCert -Force -ErrorAction SilentlyContinue
    } else {
        Write-Note "No user cert found at: $userCert"
    }

    if (-not $SkipSystemCertRemoval) {
        # If cloudflared was installed as a service, it may store cert under system profile
        $systemCert = "C:/Windows/System32/config/systemprofile/.cloudflared/cert.pem"
        if (Test-Path $systemCert) {
            try {
                Write-Note "Attempting to remove system cert: $systemCert (admin may be required)"
                Remove-Item $systemCert -Force -ErrorAction Stop
            } catch {
                Write-Warning "Couldn't remove system cert (try running as Administrator): $($_.Exception.Message)"
            }
        } else {
            Write-Note "No system cert found at: $systemCert"
        }
    }

    if ($CertPath) {
        try {
            if (Test-Path $CertPath) {
                Write-Note "Removing provided cert path: $CertPath"
                Remove-Item $CertPath -Force -ErrorAction Stop
            } else {
                Write-Note "Provided cert path not found: $CertPath"
            }
        } catch {
            Write-Warning "Couldn't remove provided cert path: $($_.Exception.Message)"
        }
    }
}

function Get-CloudflaredPath {
    if ($CloudflaredPath) {
        if (Test-Path $CloudflaredPath) { return (Resolve-Path $CloudflaredPath).Path }
        throw "Provided CloudflaredPath not found: $CloudflaredPath"
    }

    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        'C:/Program Files/Cloudflare/Cloudflare Tunnel/cloudflared.exe',
        (Join-Path $env:ProgramFiles 'Cloudflare/Cloudflare Tunnel/cloudflared.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Cloudflare/Cloudflare Tunnel/cloudflared.exe'),
        'C:/Program Files/cloudflared/cloudflared.exe',
        (Join-Path $env:ProgramFiles 'cloudflared/cloudflared.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'cloudflared/cloudflared.exe'),
        'C:/ProgramData/chocolatey/bin/cloudflared.exe',
        (Join-Path $env:ChocolateyInstall 'bin/cloudflared.exe'),
        (Join-Path $env:USERPROFILE 'scoop/shims/cloudflared.exe'),
        (Join-Path $env:USERPROFILE 'scoop/apps/cloudflared/current/cloudflared.exe'),
        (Join-Path $env:LOCALAPPDATA 'cloudflared/cloudflared.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs/cloudflared/cloudflared.exe'),
        'C:/Windows/System32/cloudflared.exe'
    )
    foreach ($p in $candidates) { if (Test-Path $p) { return $p } }

    # Fallback: light recursive search in common locations
    $searchRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA, 'C:/ProgramData') | Where-Object { $_ -and (Test-Path $_) }
    foreach ($root in $searchRoots) {
        $found = Get-ChildItem -Path $root -Filter 'cloudflared.exe' -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
        if ($found) { return $found }
    }

    throw "cloudflared not found. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/ and reopen PowerShell, or pass -CloudflaredPath."
}

try {
    Write-Note "Resetting Cloudflare Tunnel login state..."
    $cfPath = Get-CloudflaredPath
    $ver = (& $cfPath --version) 2>$null | Select-Object -First 1
    Write-Note "Using cloudflared: $cfPath - $ver"
    Stop-Cloudflared
    Remove-CertFile

    $loginArgs = @('tunnel','login')

    Write-Note "Launching: $cfPath $($loginArgs -join ' ')"
    Write-Note "If a device code is shown, open https://dash.cloudflare.com/warp and enter the code."
    & $cfPath @loginArgs

    Write-Note "If login succeeded, a new cert.pem should exist in $env:USERPROFILE/.cloudflared."
    Write-Note "Next steps examples:"
    Write-Note " - Create tunnel: `"$cfPath`" tunnel create wileywidget"
    Write-Note " - Route DNS:    `"$cfPath`" tunnel route dns wileywidget app.townofwiley.gov"
    Write-Note " - Run tunnel:   `"$cfPath`" tunnel run wileywidget"
} catch {
    Write-Error $_
    exit 1
}
