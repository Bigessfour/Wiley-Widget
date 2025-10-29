# Wiley Widget Project-Specific Profile
# Loaded only when working in the Wiley Widget project directory

#Requires -Version 7.5

# Project-specific environment
$env:WILEY_WIDGET_ROOT = $PSScriptRoot

# Project aliases for Wiley Widget development
function Invoke-DevStart { 
    & "$PSScriptRoot\.venv\Scripts\python.exe" "$PSScriptRoot\scripts\dev-start.py" @args 
}

function Invoke-CleanupDotnet { 
    & "$PSScriptRoot\.venv\Scripts\python.exe" "$PSScriptRoot\scripts\cleanup-dotnet.py" @args 
}

function Invoke-LoadEnv { 
    & "$PSScriptRoot\.venv\Scripts\python.exe" "$PSScriptRoot\scripts\load-env.py" @args 
}

# Set aliases
Set-Alias -Name 'dev-start' -Value Invoke-DevStart -Option AllScope -ErrorAction SilentlyContinue
Set-Alias -Name 'cleanup-dotnet' -Value Invoke-CleanupDotnet -Option AllScope -ErrorAction SilentlyContinue
Set-Alias -Name 'load-env' -Value Invoke-LoadEnv -Option AllScope -ErrorAction SilentlyContinue

# Navigate to project root if not already there
if ($PWD.Path -notlike "*Wiley_Widget*") {
    Set-Location $PSScriptRoot
}
