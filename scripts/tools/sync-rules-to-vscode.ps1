[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$continueRules = Join-Path $repoRoot '..' | Join-Path -ChildPath '.continue\rules'
$vscodeDir = Join-Path $repoRoot '..' | Join-Path -ChildPath '.vscode'

Write-Information "Repo root: $repoRoot" -InformationAction Continue
Write-Information "Rules src:  $continueRules" -InformationAction Continue
Write-Information "VSCode dir: $vscodeDir" -InformationAction Continue

if (-not (Test-Path $vscodeDir)) {
    New-Item -ItemType Directory -Path $vscodeDir | Out-Null
}

if (-not (Test-Path $continueRules)) {
    Write-Warning "Canonical rules directory not found: $continueRules"
    Write-Warning "Nothing to sync. Exiting."
    exit 0
}

$ruleFiles = Get-ChildItem -Path $continueRules -Filter *.md -File -ErrorAction SilentlyContinue
if (-not $ruleFiles) {
    Write-Warning "No *.md files found under $continueRules"
    exit 0
}

$changes = @()
foreach ($rule in $ruleFiles) {
    $dest = Join-Path $vscodeDir $rule.Name
    $needsCopy = $true
    if (Test-Path $dest) {
        $srcHash = Get-FileHash -Path $rule.FullName -Algorithm SHA256
        $dstHash = Get-FileHash -Path $dest -Algorithm SHA256
        if ($srcHash.Hash -eq $dstHash.Hash) {
            $needsCopy = $false
        }
    }
    if ($needsCopy) {
        $changes += @{ Name = $rule.Name; Source = $rule.FullName; Dest = $dest }
        if ($Force -or $PSCmdlet.ShouldProcess($dest, "Copy from canonical rules")) {
            Copy-Item -Path $rule.FullName -Destination $dest -Force
        }
    }
}

if ($changes.Count -eq 0) {
    Write-Host "✓ .vscode rules are already in sync with .continue/rules" -ForegroundColor Green
    exit 0
}

Write-Host "Detected rule differences:" -ForegroundColor Yellow
foreach ($c in $changes) {
    Write-Host (" - {0}" -f $c.Name) -ForegroundColor Cyan
}

if (-not $Force) {
    Write-Host "Run with -Force to apply updates:" -ForegroundColor Yellow
    Write-Host "  pwsh .\\scripts\\tools\\sync-rules-to-vscode.ps1 -Force" -ForegroundColor Gray
} else {
    Write-Host "✓ Copied updated rules to .vscode" -ForegroundColor Green
}
