<#!
.SYNOPSIS
  Manage collection and harvesting of Windows crash dumps for WileyWidget (and other processes).

.DESCRIPTION
  Provides functions to:
    * Enable or disable Windows Error Reporting (WER) LocalDumps (full dumps, DumpType=2)
    * List crash dump files for WileyWidget.exe (or any target process name)
    * Copy dump files into the repository under logs/dumps for source control assisted triage (not recommended to commit large files)
    * Summarize dump metadata (size, timestamp, age) and emit optional JSON

  All registry modifications require elevated PowerShell (Run as Administrator). If not elevated the script warns and skips registry actions.

.PARAMETER Enable
  Enable full crash dump capture (DumpType=2) with default DumpCount=10.

.PARAMETER Disable
  Remove LocalDumps configuration (does NOT delete existing dumps).

.PARAMETER List
  List existing dump files (CrashDumps + Temp). Accepts optional -ProcessName.

.PARAMETER Copy
  Copy matching dump files to repo logs/dumps (creates folder if missing). Use -Overwrite to replace existing copies.

.PARAMETER ProcessName
  Target process name (default: WileyWidget.exe). Case-insensitive. Can pass without .exe.

.PARAMETER Json
  When listing, also output a JSON summary object (for tooling).

.PARAMETER Overwrite
  Overwrite existing copied dump files in destination when used with -Copy.

.PARAMETER Destination
  Alternate destination for -Copy (default: ./logs/dumps).

.EXAMPLES
  # Enable local dump capture
  ./scripts/crash-dump-management.ps1 -Enable

  # List dumps for WileyWidget with JSON output
  ./scripts/crash-dump-management.ps1 -List -Json

  # Copy dumps to repo folder
  ./scripts/crash-dump-management.ps1 -Copy

.NOTES
  Registry Path: HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps
  Keys Set: DumpType (DWORD 2 = Full), DumpCount (DWORD 10)

#>
[CmdletBinding(DefaultParameterSetName='None')]
param(
    [Parameter(ParameterSetName='Enable')]   [switch] $Enable,
    [Parameter(ParameterSetName='Disable')]  [switch] $Disable,
    [Parameter(ParameterSetName='List')]     [switch] $List,
    [Parameter(ParameterSetName='Copy')]     [switch] $Copy,
    [string] $ProcessName = 'WileyWidget.exe',
    [switch] $Json,
    [switch] $Overwrite,
    [string] $Destination = (Join-Path -Path (Get-Location) -ChildPath 'logs/dumps')
)

function Test-IsAdmin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

function Normalize-ProcessName($name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return 'WileyWidget.exe' }
    if ($name -notmatch '\\.exe$') { return "$name.exe" }
    return $name
}

function Enable-LocalDumps {
    param([string] $TargetProcess)
    if (-not (Test-IsAdmin)) {
        Write-Warning 'Elevation required to modify LocalDumps. Re-run PowerShell as Administrator.'
        return
    }
    $baseKey = 'HKLM:SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\LocalDumps'
    if (-not (Test-Path $baseKey)) { New-Item -Path $baseKey -Force | Out-Null }

    # Optional per-process subkey (not strictly required unless customizing path or limits per process)
    $procKey = Join-Path $baseKey $TargetProcess
    if (-not (Test-Path $procKey)) { New-Item -Path $procKey -Force | Out-Null }

    New-ItemProperty -Path $baseKey -Name DumpType  -PropertyType DWord -Value 2 -Force | Out-Null
    New-ItemProperty -Path $baseKey -Name DumpCount -PropertyType DWord -Value 10 -Force | Out-Null

    Write-Host "✅ LocalDumps enabled (Full dumps) for all processes. Target process: $TargetProcess" -ForegroundColor Green
    Write-Host 'Dumps will appear under %LOCALAPPDATA%\CrashDumps after next crash.'
}

function Disable-LocalDumps {
    if (-not (Test-IsAdmin)) {
        Write-Warning 'Elevation required to disable LocalDumps.'
        return
    }
    $baseKey = 'HKLM:SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\LocalDumps'
    if (Test-Path $baseKey) {
        Remove-Item -Path $baseKey -Recurse -Force
        Write-Host '🧹 LocalDumps configuration removed.' -ForegroundColor Yellow
    } else {
        Write-Host 'No LocalDumps configuration present.'
    }
}

function Get-DumpFilesInternal {
    param([string] $TargetProcess)
    $procBasename = [IO.Path]::GetFileNameWithoutExtension($TargetProcess)
    $locations = @(
        Join-Path $Env:LOCALAPPDATA 'CrashDumps'
        Join-Path $Env:LOCALAPPDATA 'Temp'
    ) | Where-Object { Test-Path $_ }

    $patterns = @(
        "$procBasename*.dmp",
        "$procBasename*.mdmp",
        '*.dmp','*.mdmp','*.hdmp'
    )

    $files = foreach ($loc in $locations) {
        foreach ($pat in $patterns) {
            Get-ChildItem -Path $loc -Filter $pat -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer }
        }
    }

    $files | Sort-Object LastWriteTime -Descending -Unique
}

function Show-DumpList {
    param([string] $TargetProcess, [switch] $AsJson)
    $files = Get-DumpFilesInternal -TargetProcess $TargetProcess
    if (-not $files) {
        Write-Host 'No dump files found.' -ForegroundColor Yellow
        if ($AsJson) { '{}' | Write-Output }
        return
    }

    $summary = $files | Select-Object @{n='File';e={$_.FullName}},
                                   @{n='SizeMB';e={[math]::Round(($_.Length/1MB),2)}},
                                   LastWriteTime,
                                   @{n='Age';e={(New-TimeSpan -Start $_.LastWriteTime -End (Get-Date)).ToString()}}

    $summary | Format-Table -AutoSize

    if ($AsJson) {
        $summary | ConvertTo-Json -Depth 3 | Write-Output
    }
}

function Copy-Dumps {
    param([string] $TargetProcess, [string] $DestinationPath, [switch] $Overwrite)
    $files = Get-DumpFilesInternal -TargetProcess $TargetProcess
    if (-not $files) {
        Write-Warning 'No dump files to copy.'
        return
    }
    if (-not (Test-Path $DestinationPath)) { New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null }

    foreach ($f in $files) {
        $dest = Join-Path $DestinationPath $f.Name
        if ((Test-Path $dest) -and -not $Overwrite) {
            Write-Host "Skip (exists): $($f.Name)" -ForegroundColor DarkYellow
            continue
        }
        Copy-Item -Path $f.FullName -Destination $dest -Force
        Write-Host "Copied: $($f.Name) -> $DestinationPath" -ForegroundColor Cyan
    }
}

# --- MAIN EXECUTION ---
$normalized = Normalize-ProcessName $ProcessName

switch ($PsCmdlet.ParameterSetName) {
    'Enable' { Enable-LocalDumps -TargetProcess $normalized; break }
    'Disable'{ Disable-LocalDumps; break }
    'List'   { Show-DumpList -TargetProcess $normalized -AsJson:$Json; break }
    'Copy'   { Copy-Dumps -TargetProcess $normalized -DestinationPath $Destination -Overwrite:$Overwrite; break }
    Default  {
        Write-Host 'Usage examples:' -ForegroundColor Cyan
        Write-Host '  Enable full dumps:  ./scripts/crash-dump-management.ps1 -Enable'
        Write-Host '  Disable dumps:      ./scripts/crash-dump-management.ps1 -Disable'
        Write-Host '  List dumps (JSON):  ./scripts/crash-dump-management.ps1 -List -Json'
        Write-Host '  Copy dumps:         ./scripts/crash-dump-management.ps1 -Copy -Destination logs/dumps'
        Write-Host '  Target other proc:  ./scripts/crash-dump-management.ps1 -List -ProcessName SomeApp'
    }
}
