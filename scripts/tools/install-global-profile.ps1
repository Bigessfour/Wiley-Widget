Param(
    [switch]$Force
)

$profilePath = [string]$PROFILE.CurrentUserAllHosts
if (-not $profilePath) {
    Write-Error "Unable to determine CurrentUserAllHosts profile path."
    exit 1
}

$timestamp = (Get-Date).ToString('yyyyMMddTHHmmssZ')
$backupPath = "$profilePath.bak.$timestamp"

if (Test-Path $profilePath) {
    Copy-Item -Path $profilePath -Destination $backupPath -Force
    Write-Host "Backed up existing profile to: $backupPath"
} else {
    $parent = Split-Path $profilePath -Parent
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        Write-Host "Created parent directory: $parent"
    }
}

# Content to install into the CurrentUserAllHosts profile
$profileContent = @'
# ──────────────────────────────────────────────────────────────
# Global PowerShell profile — runs for ALL PowerShell sessions
# (Critical for VS Code + MCP + Copilot sentinel)
# ──────────────────────────────────────────────────────────────

# Wiley Widget workspace auto-loader (runs even when VS Code spawns plain pwsh)
$wwRoot = "C:\Users\biges\Desktop\Wiley-Widget"
if (Test-Path "$wwRoot\.vscode\startup.ps1") {
    . "$wwRoot\.vscode\startup.ps1"
}

# Force MCP environment variables (fallback if startup.ps1 fails)
$env:WW_REPO_ROOT     = $wwRoot
$env:CSX_ALLOWED_PATH = "$wwRoot\scripts"
$env:WW_LOGS_DIR      = "$wwRoot\logs"

# Load Copilot sentinel wrapper (so terminal never hangs again)
$sentinel = "$wwRoot\.vscode\profile-copilot-sentinel.ps1"
if (Test-Path $sentinel) { . $sentinel }

Write-Host "Wiley Widget MCP environment loaded (global profile)" -ForegroundColor DarkCyan
'@

# Write the content
Set-Content -Path $profilePath -Value $profileContent -Encoding UTF8 -Force
Write-Host "Installed new profile to: $profilePath"
Write-Host "To apply now, run: `. $profilePath` (or start a new PowerShell session)" -ForegroundColor Yellow
