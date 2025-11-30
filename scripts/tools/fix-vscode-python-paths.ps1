<#
NAME
    fix-vscode-python-paths.ps1

SYNOPSIS
    Detect VS Code workspace settings pointing to Python Store / old python3.11 paths and optionally replace them
    with the project's workspace venv Python interpreter (\.venv\Scripts\python.exe) when available.

USAGE
    .\fix-vscode-python-paths.ps1 [-Path <string>] [-AutoFix] [-BackupDir <string>] [-WhatIf]

DESCRIPTION
    This script scans for .vscode/settings.json files under the provided path (defaults to repository root)
    and looks for settings that reference deleted Microsoft Store Python installations or older explicit
    interpreter paths (e.g., Python.3.11 or python3.11). If a workspace .venv is found it will replace
    those references with the workspace venv's python.exe. It will create a backup of any modified settings
    file before applying changes.

    This is intended to help devs after upgrading the repo's standard Python to 3.14 and removing old
    Store-distributed interpreters.

EXAMPLES
    # Dry run (default) — discover issues but don't change files
    .\fix-vscode-python-paths.ps1 -Path ..\ -WhatIf

    # Auto-fix detected problems, backing up originals into .backup\vscode-fix
    .\fix-vscode-python-paths.ps1 -Path ..\ -AutoFix -BackupDir .backup\vscode-fix

#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [string]$Path = (Get-Location).Path,
    [switch]$AutoFix,
    [string]$BackupDir = "$PSScriptRoot/../.backup/vscode-fix",
    [switch]$WhatIf
)

function Resolve-VenvPython {
    param([string]$workspaceRoot)
    $venv = Join-Path -Path $workspaceRoot -ChildPath ".venv\Scripts\python.exe"
    if (Test-Path $venv) { return (Resolve-Path $venv).Path }
    # Other common venv location: venv\Scripts\python.exe
    $venv2 = Join-Path -Path $workspaceRoot -ChildPath "venv\Scripts\python.exe"
    if (Test-Path $venv2) { return (Resolve-Path $venv2).Path }
    return $null
}

Write-Host "Searching for .vscode/settings.json under: $Path" -ForegroundColor Cyan

$settingsFiles = Get-ChildItem -Path $Path -Recurse -Filter settings.json -ErrorAction SilentlyContinue | Where-Object { $_.DirectoryName -match '\\.vscode$' }
if (!$settingsFiles) {
    Write-Host 'No .vscode/settings.json files found.' -ForegroundColor Yellow
    return
}

$storePattern = '\\(Microsoft\\WindowsApps\\|PythonSoftwareFoundation\.Python|python3\.11|python311|Python\.3\.11|python3.11)'
$changed = 0

foreach ($file in $settingsFiles) {
    $json = Get-Content -Raw -Path $file.FullName -ErrorAction SilentlyContinue
    if (-not $json) { continue }

    if ($json -match $storePattern) {
        Write-Host "Found potential Store/old-Python reference in: $($file.FullName)" -ForegroundColor Magenta
        $workspaceRoot = (Resolve-Path ($file.Directory.Parent.FullName)).Path
        $venvPython = Resolve-VenvPython -workspaceRoot $workspaceRoot

        if ($venvPython) {
            Write-Host "  Workspace venv found: $venvPython" -ForegroundColor Green

            if ($AutoFix) {
                if ($PSCmdlet.ShouldProcess($file.FullName, 'Replace Store/old interpreter path with workspace .venv python')) {
                    $backupPath = Join-Path -Path $BackupDir -ChildPath (Split-Path -Leaf $file.FullName)
                    if (-not (Test-Path -Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null }
                    Copy-Item -Path $file.FullName -Destination $backupPath -Force

                    # Replace known settings keys
                    $newJson = $json -replace '"python.defaultInterpreterPath"\s*:\s*"[^"]+"', "`"python.defaultInterpreterPath`": `"$venvPython`""
                    $newJson = $newJson -replace '"python.pythonPath"\s*:\s*"[^"]+"', "`"python.pythonPath`": `"$venvPython`""
                    # Some users use interpreterPath
                    $newJson = $newJson -replace '"python\.analysis\.pythonPath"\s*:\s*"[^"]+"', "`"python.analysis.pythonPath`": `"$venvPython`""

                    Set-Content -Path $file.FullName -Value $newJson -Force
                    Write-Host "  Replaced settings and backed up original to $backupPath" -ForegroundColor Green
                    $changed++
                }
            }
            else {
                Write-Host "  To auto-fix, re-run with -AutoFix. Detected workspace venv at: $venvPython" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  No workspace venv detected. Consider creating one at $workspaceRoot\.venv and re-run with -AutoFix." -ForegroundColor Yellow
        }
    }
}

if ($changed -eq 0) {
    Write-Host "No files were modified." -ForegroundColor Yellow
} else {
    Write-Host "Modified $changed settings.json file(s)." -ForegroundColor Green
}
