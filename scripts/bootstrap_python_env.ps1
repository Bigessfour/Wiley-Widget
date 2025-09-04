<#
Bootstrap Python environment for WileyWidget.
Sets up a local .venv, installs dependencies, and prints next steps.
#>
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

Write-Host "[Python Bootstrap] Starting (auto-detecting Python)" -ForegroundColor Cyan

if (Test-Path .venv) {
    if ($Force) {
        Write-Host "[Python Bootstrap] Removing existing .venv (Force)" -ForegroundColor Yellow
        Remove-Item -Recurse -Force .venv
    } else {
        Write-Host "[Python Bootstrap] Existing .venv found. Use -Force to recreate." -ForegroundColor Green
    }
} else {
    Write-Host "[Python Bootstrap] Creating virtual environment (.venv)" -ForegroundColor Cyan
    function Get-PythonInvoker {
        $py = Get-Command py -ErrorAction SilentlyContinue
        if ($py) { return 'py -3' }
        $python = Get-Command python -ErrorAction SilentlyContinue
        if ($python) { return 'python' }
        return $null
    }
    $invoker = Get-PythonInvoker
    if (-not $invoker) {
        Write-Host "[Python Bootstrap] ERROR: Neither 'py' nor 'python' was found on PATH. Install Python 3.10+ from https://www.python.org/downloads/ and re-run." -ForegroundColor Red
        exit 1
    }
    Write-Host "[Python Bootstrap] Using invoker: $invoker" -ForegroundColor DarkCyan
    & $invoker -m venv .venv
}

Write-Host "[Python Bootstrap] Activating .venv" -ForegroundColor Cyan
$activate = Join-Path .venv Scripts Activate.ps1
. $activate

Write-Host "[Python Bootstrap] Upgrading pip" -ForegroundColor Cyan
python -m pip install --upgrade pip --quiet || Write-Host "[Python Bootstrap] (Warning) pip upgrade failed, continuing" -ForegroundColor Yellow

if (Test-Path scripts/requirements.txt) {
    Write-Host "[Python Bootstrap] Installing dependencies" -ForegroundColor Cyan
    python -m pip install -r scripts/requirements.txt
} else {
    Write-Host "[Python Bootstrap] No requirements.txt found, skipping installs" -ForegroundColor Yellow
}

Write-Host "[Python Bootstrap] Verifying debugpy shim availability (provided by VS Code extension)" -ForegroundColor Cyan
try {
    debugpy -h | Out-Null
    Write-Host "[Python Bootstrap] debugpy available (no-config debugging ready)" -ForegroundColor Green
} catch {
    Write-Host "[Python Bootstrap] debugpy not found in this terminal. Open a NEW integrated terminal after activation." -ForegroundColor Yellow
}

Write-Host "[Python Bootstrap] Done." -ForegroundColor Cyan
Write-Host "[Python Bootstrap] NOTE: No custom 'py' launcher needed; we auto-detected available Python." -ForegroundColor Gray
Write-Host "Next steps:" -ForegroundColor Magenta
Write-Host "  1. Set a breakpoint in a script under scripts/" -ForegroundColor Magenta
Write-Host "  2. Run: debugpy scripts/project-analyzer.py" -ForegroundColor Magenta
Write-Host "  3. (Optional) Add args after script name" -ForegroundColor Magenta
