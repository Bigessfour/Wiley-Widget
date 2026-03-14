# Wiley Widget Project-Specific Profile
# Loaded only when working in the Wiley Widget project directory

#Requires -Version 7.5.4

# Project-specific environment
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$env:WILEY_WIDGET_ROOT = $projectRoot
$env:WW_REPO_ROOT = $projectRoot
$env:CSX_ALLOWED_PATH = $projectRoot
$env:WW_LOGS_DIR = Join-Path $projectRoot 'logs'
$env:WILEYWIDGET_LOG_DIR = Join-Path $projectRoot 'logs'

# Project aliases for Wiley Widget development
function Invoke-DevStart {
    & "$projectRoot\.venv\Scripts\python.exe" "$projectRoot\scripts\dev-start.py" @args
}

function Invoke-CleanupDotnet {
    & "$projectRoot\.venv\Scripts\python.exe" "$projectRoot\scripts\cleanup-dotnet.py" @args
}

function Invoke-LoadEnv {
    & "$projectRoot\.venv\Scripts\python.exe" "$projectRoot\scripts\load-env.py" @args
}

# Set aliases
Set-Alias -Name 'dev-start' -Value Invoke-DevStart -Option AllScope -ErrorAction SilentlyContinue
Set-Alias -Name 'cleanup-dotnet' -Value Invoke-CleanupDotnet -Option AllScope -ErrorAction SilentlyContinue
Set-Alias -Name 'load-env' -Value Invoke-LoadEnv -Option AllScope -ErrorAction SilentlyContinue

# Navigate to project root if not already there
if (-not $PWD.Path.StartsWith($projectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    Set-Location $projectRoot
}
