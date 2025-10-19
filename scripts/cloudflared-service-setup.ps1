# Requires: PowerShell 7+
param(
    [string]$TunnelId = 'ddd24f98-673d-43cb-b8a8-21a2329fffec',
    [string]$ConfigPath = 'C:\ProgramData\cloudflared\config.yml',
    [string]$CloudflaredPath = 'C:\Program Files (x86)\cloudflared\cloudflared.exe',
    [string]$ServiceName = 'cloudflared-wileywidget',
    [switch]$Force
)

function Assert-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must be run in an elevated PowerShell (Run as Administrator).'
    }
}

function Test-File([string]$Path) {
    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        throw "Required file not found: $Path"
    }
}

function Get-CredentialsFilePath([string]$TunnelId) {
    $p = Join-Path 'C:\ProgramData\cloudflared' ("$TunnelId.json")
    return $p
}

function Install-CloudflaredService {
    param(
        [string]$ServiceName,
        [string]$CloudflaredPath,
        [string]$ConfigPath,
        [string]$TunnelId,
        [switch]$Force
    )

    if (-not (Test-Path $CloudflaredPath)) {
        throw "cloudflared not found at: $CloudflaredPath"
    }
    Test-File -Path $ConfigPath
    $credPath = Get-CredentialsFilePath -TunnelId $TunnelId
    Test-File -Path $credPath

    $bin = '"' + $CloudflaredPath + '"'
    $svcArgs = "tunnel --config `"$ConfigPath`" run $TunnelId"
    $binaryPathName = "$bin $svcArgs"

    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        if ($Force) {
            Write-Output "Service $ServiceName exists; removing due to -Force."
            Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue | Out-Null
            sc.exe delete $ServiceName | Out-Null
            Start-Sleep -Seconds 1
        } else {
            Write-Output "Service $ServiceName already exists. Updating binpath..."
            sc.exe config $ServiceName binPath= $binaryPathName | Out-Null
            sc.exe config $ServiceName start= delayed-auto | Out-Null
            return
        }
    }

    Write-Output "Creating service $ServiceName"
    New-Service -Name $ServiceName -BinaryPathName $binaryPathName -DisplayName 'Cloudflared (WileyWidget Tunnel)' -StartupType Automatic | Out-Null
    # Delayed auto start improves reliability at boot
    sc.exe config $ServiceName start= delayed-auto | Out-Null
}

function Start-CloudflaredService {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$ServiceName)
    if ($PSCmdlet.ShouldProcess($ServiceName, 'Start service')) {
        Write-Output "Starting $ServiceName"
        Start-Service -Name $ServiceName
        Start-Sleep -Seconds 2
        $svc = Get-Service -Name $ServiceName -ErrorAction Stop
        if ($svc.Status -ne 'Running') {
            throw "$ServiceName failed to start. Current state: $($svc.Status)"
        }
    }
}

function Test-Tunnel {
    param([string]$CloudflaredPath, [string]$TunnelId)
    Write-Output "Checking tunnel status for $TunnelId"
    $p = Start-Process -FilePath $CloudflaredPath -ArgumentList @('tunnel','info',$TunnelId) -NoNewWindow -PassThru -Wait -RedirectStandardOutput (New-TemporaryFile).FullName -RedirectStandardError (New-TemporaryFile).FullName
    if ($p.ExitCode -ne 0) {
        Write-Warning "cloudflared tunnel info exited with code $($p.ExitCode)."
    } else {
        Write-Output "cloudflared tunnel info executed successfully."
    }
}

try {
    Assert-Admin
    Write-Output "Validating files..."
    Test-File -Path $CloudflaredPath
    Test-File -Path $ConfigPath
    Test-File -Path (Get-CredentialsFilePath -TunnelId $TunnelId)

    Install-CloudflaredService -ServiceName $ServiceName -CloudflaredPath $CloudflaredPath -ConfigPath $ConfigPath -TunnelId $TunnelId -Force:$Force
    Start-CloudflaredService -ServiceName $ServiceName
    Test-Tunnel -CloudflaredPath $CloudflaredPath -TunnelId $TunnelId

    Write-Output "SUCCESS: Service '$ServiceName' is running."
    Write-Output "Tip: Verify public health: https://app.townofwiley.gov/health"
}
catch {
    Write-Error $_
    exit 1
}
