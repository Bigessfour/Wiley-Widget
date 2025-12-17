<#
.SYNOPSIS
  Install Trunk CLI (via npm @trunkio/launcher) and optionally install the VS Code Trunk extension.

.DESCRIPTION
  This helper script attempts to install the Trunk CLI globally using npm and verifies the installed version
  meets the minimum declared in `.trunk/trunk.yaml` (1.25.0). It can also attempt to install the VS Code
  extension if the VS Code `code` CLI is available.

.PARAMETER InstallVSCodeExtension
  If specified, the script will attempt to install the VS Code extension `trunkio.trunk` using `code`.

.EXAMPLE
  pwsh .\scripts\trunk\setup-trunk.ps1 -InstallVSCodeExtension
#>
param(
    [switch]$InstallVSCodeExtension
)

$ErrorActionPreference = 'Stop'
$requiredVersion = [Version]"1.25.0"

function Write-Info($msg) { Write-Host $msg -ForegroundColor Green }
function Write-Warn($msg) { Write-Host $msg -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host $msg -ForegroundColor Red }

Write-Info "Checking for Node/npm..."
$npm = Get-Command npm -ErrorAction SilentlyContinue
if (-not $npm)
{
    Write-Err "npm not found. Please install Node.js and npm first: https://nodejs.org/"
    exit 2
}

# Quick check if trunk is already installed and meets version
function Get-TrunkVersion {
    try {
        $v = (& trunk --version 2>$null | Select-Object -First 1)
        if (-not $v) { return $null }
        # Example output: trunk version 1.25.0
        if ($v -match '([0-9]+\.[0-9]+\.[0-9]+)') { return [Version]$matches[1] }
    }
    catch { return $null }
    return $null
}

$current = Get-TrunkVersion
if ($current -ne $null -and $current -ge $requiredVersion) {
    Write-Info "Found trunk CLI version $current >= $requiredVersion. Skipping installation."
}
else {
    Write-Info "Installing Trunk CLI globally via npm (\"@trunkio/launcher\")."
    try {
        npm install -g @trunkio/launcher
    }
    catch {
        Write-Err "Failed to install @trunkio/launcher via npm. You may need elevated permissions or to use nvm. Error: $_"
        exit 3
    }

    $new = Get-TrunkVersion
    if ($new -eq $null) {
        Write-Err "Installation did not expose 'trunk' in PATH. Ensure your global npm bin directory is on PATH."
        exit 4
    }
    if ($new -lt $requiredVersion) {
        Write-Warn "Installed trunk version $new is less than required $requiredVersion. Behavior may be unsupported."
    }
    else {
        Write-Info "Installed trunk CLI version $new"
    }
}

if ($InstallVSCodeExtension) {
    Write-Info "Attempting to install VS Code extension 'trunkio.trunk' via 'code' CLI (if available)..."
    $code = Get-Command code -ErrorAction SilentlyContinue
    if (-not $code) {
        Write-Warn "VS Code 'code' CLI not found. Please install the extension manually from the Marketplace: https://marketplace.visualstudio.com/search?q=trunk"
    }
    else {
        try {
            & code --install-extension trunkio.trunk --force
            Write-Info "Installed 'trunkio.trunk' extension (or already present)."
        }
        catch {
            Write-Warn "Failed to install Trunk extension via 'code'. You can install it from the Marketplace manually. Error: $_"
        }
    }
}

Write-Info "Setup complete. Next step: authenticate with Trunk (interactive):"
Write-Host "  trunk login" -ForegroundColor Cyan
Write-Host "Then you can run checks like: 'trunk check --ci'" -ForegroundColor Cyan
