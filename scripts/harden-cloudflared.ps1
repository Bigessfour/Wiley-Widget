param(
    [string]$ServiceName = 'cloudflared-wileywidget',
    [string]$ProgramDataDir = 'C:\\ProgramData\\cloudflared',
    [switch]$OnlyFileAcls,
    [switch]$UseServiceSid
)

function Assert-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must be run in an elevated PowerShell (Run as Administrator).'
    }
}

try {
    Assert-Admin

    if (-not (Test-Path $ProgramDataDir)) {
        throw "Directory not found: $ProgramDataDir"
    }

    # Ensure a logs directory for write access (principle of least privilege)
    $logsDir = Join-Path $ProgramDataDir 'logs'
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }

    # Switch service to LocalService (least privilege built-in account)
    Write-Output "Setting service '$ServiceName' logon to LocalService"
    sc.exe config $ServiceName obj= "NT AUTHORITY\LocalService" password= "" | Out-Null

    # Enable Service SID for targeted ACLs
    if ($UseServiceSid) {
        Write-Output "Enabling Service SID for '$ServiceName' (unrestricted)"
        sc.exe sidtype $ServiceName unrestricted | Out-Null
    }

    # Tighten ACLs on ProgramData\cloudflared
    Write-Output "Hardening ACLs: $ProgramDataDir"
    icacls $ProgramDataDir /inheritance:r | Out-Null
    icacls $ProgramDataDir /grant:r "Administrators:(OI)(CI)(F)" | Out-Null

    $principal = if ($UseServiceSid) { "NT SERVICE\$ServiceName" } else { "NT AUTHORITY\LocalService" }

    # Grant Read/Execute to base folder (config/credentials)
    icacls $ProgramDataDir /grant "${principal}:(OI)(CI)(RX)" | Out-Null
    # Grant Modify to logs folder only
    icacls $logsDir /grant "${principal}:(OI)(CI)(M)" | Out-Null

    if ($OnlyFileAcls) {
        Write-Output "ACL hardening complete (OnlyFileAcls)."
        exit 0
    }

    # Configure service recovery to auto-restart
    Write-Output "Configuring service recovery: $ServiceName"
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

    # Ensure delayed auto start for reliability at boot
    Write-Output "Setting Delayed Auto Start"
    sc.exe config $ServiceName start= delayed-auto | Out-Null

    Write-Output "Hardening complete."
}
catch {
    Write-Error $_
    exit 1
}
