#Requires -Version 7.0

<#
.SYNOPSIS
    Dumps the UI Automation tree of the WileyWidget application for debugging UI tests (PowerShell 7 compatible).

.DESCRIPTION
    This script starts the WileyWidget application, waits for it to load, and then dumps the automation tree to the console.
    Uses FlaUI (modern .NET-compatible UI Automation library) instead of legacy UIAutomationClient.
    Useful for identifying AutomationIds and structure for UI automation tests.

.PARAMETER Timeout
    Timeout in seconds to wait for the main window. Default is 30.

.PARAMETER OutputFile
    Optional file to save the tree dump. If not specified, outputs to console.

.EXAMPLE
    .\dump-ui-tree.ps1
.EXAMPLE
    .\dump-ui-tree.ps1 -Timeout 60 -OutputFile tree.txt

.NOTES
    Requires FlaUI.Core.dll and FlaUI.UIA3.dll in a sibling 'lib' folder.
    Download from NuGet: https://www.nuget.org/packages/FlaUI.Core and https://www.nuget.org/packages/FlaUI.UIA3
#>

param(
    [int]$Timeout = 30,  # Reduced for faster iteration
    [string]$OutputFile,
    [switch]$InstallDependencies,
    [string]$FlaUIVersion = "4.0.1"
)

$scriptPath = $PSScriptRoot
$libPath = Join-Path $scriptPath "lib"

function Install-FlaUIPackage {
    param([string]$Version = $FlaUIVersion)

    Write-Information "Installing FlaUI packages version $Version to '$libPath'..." -InformationAction Continue
    if (-not (Test-Path $libPath)) { New-Item -ItemType Directory -Path $libPath | Out-Null }

    $packages = @("FlaUI.Core","FlaUI.UIA3")
    foreach ($pkg in $packages) {
        $url = "https://www.nuget.org/api/v2/package/$pkg/$Version"
        $tmp = Join-Path $env:TEMP ("$pkg.$Version.nupkg")
        try {
            Write-Information "Downloading $pkg..." -InformationAction Continue
            Invoke-WebRequest -Uri $url -OutFile $tmp -ErrorAction Stop

            $dest = Join-Path $libPath $pkg
            if (Test-Path $dest) { Remove-Item -Recurse -Force $dest -ErrorAction SilentlyContinue }
            Expand-Archive -Path $tmp -DestinationPath $dest -Force

            # Copy platform-specific DLLs into the root lib folder for easy loading
            $possiblePaths = @("lib/netstandard2.0","lib/net462","lib/netstandard2.1")
            foreach ($p in $possiblePaths) {
                $dlls = Get-ChildItem -Path (Join-Path $dest $p) -Filter *.dll -ErrorAction SilentlyContinue
                foreach ($d in $dlls) {
                    Copy-Item -Path $d.FullName -Destination (Join-Path $libPath $d.Name) -Force
                }
            }
        }
        catch {
            Write-Warning "Failed to install ${pkg}: $($_.Exception.Message)"
            return $false
        }
        finally {
            if (Test-Path $tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
        }
    }

    return $true
}

# Ensure required DLLs are present
$requiredDlls = @("FlaUI.Core.dll","FlaUI.UIA3.dll")
$missing = @()
foreach ($dll in $requiredDlls) {
    if (-not (Test-Path (Join-Path $libPath $dll))) { $missing += $dll }
}

if ($missing.Count -gt 0) {
    Write-Information "Missing FlaUI assemblies: $($missing -join ', ')" -InformationAction Continue
    if ($InstallDependencies) {
        if (-not (Install-FlaUIPackage -Version $FlaUIVersion)) {
            Write-Error "Automatic installation of FlaUI dependencies failed."
            exit 1
        }
    }
    else {
        Write-Error "Failed to find required FlaUI DLLs in '$libPath'. Run with -InstallDependencies to attempt automatic install or place the DLLs there manually."
        exit 1
    }
}

try {
    Add-Type -Path (Join-Path $libPath "FlaUI.Core.dll")
    Add-Type -Path (Join-Path $libPath "FlaUI.UIA3.dll")
}
catch {
    Write-Error "Failed to load FlaUI DLLs from '$libPath'. Ensure FlaUI.Core.dll and FlaUI.UIA3.dll are present and compatible with PowerShell 7."
    exit 1
}

$automation = [FlaUI.UIA3.UIA3Automation]::new()

function Get-AutomationTree {
    param(
        [FlaUI.Core.AutomationElements.AutomationElement]$Element,
        [Parameter(Mandatory)]
        $Automation,
        [int]$Depth = 0,
        [ref]$Lines
    )

    $indent = "  " * $Depth
    if (-not $Element) { return }
    $name = $Element.Name ?? ""
    $aid = $Element.AutomationId ?? ""
    $class = $Element.ClassName ?? ""
    $controlType = "Unknown"
    try {
        if ($Element.ControlType -and $Element.ControlType.ProgrammaticName) {
            $controlType = $Element.ControlType.ProgrammaticName.Split('.')[-1]
        }
    } catch {
        # Silently handle null/missing ControlType properties
    }

    $line = "$indent$controlType | Name: '$name' | Aid: '$aid' | Class: '$class'"
    $Lines.Value += $line

    # Get children - use FindAllChildren for simplicity and reliability
    try {
        $children = $Element.FindAllChildren()
        foreach ($child in $children) {
            if ($child) {
                Get-AutomationTree -Element $child -Automation $Automation -Depth ($Depth + 1) -Lines $Lines
            }
        }
    } catch {
        # Silently handle children enumeration errors
    }
}

function Invoke-DumpUiTree {
    $startTime = Get-Date
    Write-Information "Starting WileyWidget at $($startTime.ToString('HH:mm:ss'))..." -InformationAction Continue
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" `
        -PassThru `
        -WorkingDirectory (Join-Path $PSScriptRoot "..\..")

    Write-Information "Process PID: $($process.Id)" -InformationAction Continue

    try {
        # Attach to the running process (robust)
        try {
            $app = [FlaUI.Core.Application]::Attach($process)
        }
        catch {
            try {
                $app = [FlaUI.Core.Application]::Attach($process.Id)
            }
            catch {
                $fallback = Get-Process -Name "WileyWidget","WileyWidget.WinForms" -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($fallback) {
                    Write-Information "Attaching to detected WileyWidget process PID $($fallback.Id)..." -InformationAction Continue
                    $app = [FlaUI.Core.Application]::Attach($fallback)
                    $process = $fallback
                }
                else {
                    Write-Error "Failed to attach to the started process and no running 'WileyWidget' processes found: $($_.Exception.Message)"
                    exit 1
                }
            }
        }

        # Enhanced wait + fallback search
        Write-Information "Waiting for main window (timeout: $Timeout seconds)..." -InformationAction Continue
        $mainWindow = $app.GetMainWindow($automation, [TimeSpan]::FromSeconds($Timeout))

        if (-not $mainWindow) {
            Write-Warning "Main window timeout. Dumping all top-level windows..."
            # Fallback: Search all desktop windows
            $desktop = $automation.GetDesktop()
            $allWindows = $desktop.FindAll([FlaUI.Core.Definitions.TreeScope]::Children,
                $automation.ConditionFactory.ByControlType([FlaUI.Core.Definitions.ControlType]::Window))

            Write-Information "Found $($allWindows.Length) top-level windows:" -InformationAction Continue
            foreach ($win in $allWindows) {
                $title = $win.Name
                $aid = $win.AutomationId
                $class = $win.ClassName
                if ($title -match "Wiley|Widget" -or $class -match "WindowsForms") {
                    Write-Information "  CANDIDATE: '$title' | Aid: '$aid' | Class: '$class'" -InformationAction Continue
                } else {
                    Write-Output "  '$title' | Aid: '$aid' | Class: '$class'"
                }
            }
            # Auto-select best candidate (MainForm Aid)
            $mainWindow = $allWindows | Where-Object { $_.AutomationId -eq 'MainForm' } | Select-Object -First 1
            if ($mainWindow) {
                Write-Information "Selected fallback main window: '$($mainWindow.Name)' | Aid: '$($mainWindow.AutomationId)' | Class: '$($mainWindow.ClassName)'" -InformationAction Continue
            } else {
                Write-Warning "No suitable fallback window found (Aid: MainForm)."
                return  # Exit early on fail
            }
        }

        Write-Information "Found main window: $($mainWindow.Name)" -InformationAction Continue

        # Wait for UI to stabilize and allow panels to load
        Write-Information "Waiting for panels to load..." -InformationAction Continue
        Start-Sleep -Seconds 2

        # Try to click Dashboard button to force panel loading (optional - helps capture panel content)
        try {
            $navButtons = $mainWindow.FindAllDescendants()
            $dashBtn = $navButtons | Where-Object { $_.Name -eq 'Nav_Dashboard' } | Select-Object -First 1
            if ($dashBtn) {
                Write-Information "Clicking Dashboard button to load panel..." -InformationAction Continue
                try { $dashBtn.Click() } catch { }
                Start-Sleep -Seconds 2  # Wait for panel to render
            }
        }
        catch {
            Write-Information "Could not click Dashboard button: $($_.Exception.Message)" -InformationAction Continue
        }

        # Dump the tree - collect lines
        $treeLines = @()
        Get-AutomationTree -Element $mainWindow -Automation $automation -Lines ([ref]$treeLines)
        Write-Information "Captured $($treeLines.Count) tree lines." -InformationAction Continue

        $treeData = @{
            Timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
            MainWindow = @{
                Name = $mainWindow.Name
                Aid = $mainWindow.AutomationId
                Class = $mainWindow.ClassName
            }
            Tree = $treeLines
        }

        if ($OutputFile) {
            # Ensure tmp dir
            $outDir = Split-Path $OutputFile -Parent
            if ($outDir -and -not (Test-Path $outDir)) {
                New-Item -ItemType Directory -Path $outDir -Force | Out-Null
                Write-Information "Created directory: $outDir" -InformationAction Continue
            }
            $treeData | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputFile -Encoding UTF8
            Write-Information "JSON tree dump saved to $OutputFile (lines: $($treeLines.Count))" -InformationAction Continue
        }
        else {
            $treeData | ConvertTo-Json -Depth 10
        }
    }
    finally {
        # Robust cleanup
        Write-Information "Closing application..." -InformationAction Continue
        try {
            if ($app -and $process) {
                $app.Close()
                # Check if process still exists before calling WaitForExit
                try {
                    if ($null -ne $process.Id -and -not $process.HasExited) {
                        if (-not $process.WaitForExit(5000)) {  # 5s grace
                            Write-Warning "App close timeout; force closing."
                            if (-not $process.HasExited) {
                                $process.CloseMainWindow()
                                if (-not $process.WaitForExit(3000)) {
                                    Write-Warning "Force kill PID $($process.Id)."
                                    if (-not $process.HasExited) {
                                        $process.Kill()
                                    }
                                }
                            }
                        }
                    }
                }
                catch [System.InvalidOperationException] {
                    Write-Information "Process already exited." -InformationAction Continue
                }
            }
            Write-Information "Application exited successfully." -InformationAction Continue
        }
        catch {
            Write-Warning "Cleanup error: $($_.Exception.Message)"
        }
        finally {
            if ($automation) { $automation.Dispose() }
        }
    }
}

# Run the script's main action only when executed directly (not dot-sourced)
if ($MyInvocation.InvocationName -ne '.') {
    if (-not $OutputFile) { $OutputFile = "tmp/tree.json" }  # Default to JSON
    Invoke-DumpUiTree
}
