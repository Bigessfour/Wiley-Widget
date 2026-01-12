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
    [int]$Timeout = 30,
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
        [int]$Depth = 0
    )

    $indent = "  " * $Depth
    $name = $Element.Name
    $aid = $Element.AutomationId
    $class = $Element.ClassName
    $controlType = $Element.ControlType.ProgrammaticName.Split('.')[-1]  # e.g., "Window" instead of full namespace

    $line = "$indent$controlType | Name: '$name' | Aid: '$aid' | Class: '$class'"
    Write-Output $line

    # Get children (equivalent to TreeScope.Children)
    $children = $Element.FindAll([FlaUI.Core.Definitions.TreeScope]::Children, $automation.ConditionFactory.TrueCondition)
    foreach ($child in $children) {
        Get-AutomationTree -Element $child -Depth ($Depth + 1)
    }
}

function Invoke-DumpUiTree {
    # Start the application
    Write-Information "Starting WileyWidget application..." -InformationAction Continue
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project src/WileyWidget.WinForms/WileyWidget.WinForms.csproj" `
        -PassThru `
        -WorkingDirectory (Join-Path $PSScriptRoot "..\..")

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

        # Wait for the main window (automatic timeout handling)
        Write-Information "Waiting for main window (timeout: $Timeout seconds)..." -InformationAction Continue
        $mainWindow = $app.GetMainWindow($automation, [TimeSpan]::FromSeconds($Timeout))

        if (-not $mainWindow) {
            Write-Error "Failed to find main window within $Timeout seconds."
            exit 1
        }

        Write-Information "Found main window: $($mainWindow.Name)" -InformationAction Continue

        # Dump the tree
        $treeOutput = Get-AutomationTree -Element $mainWindow

        if ($OutputFile) {
            $treeOutput | Out-File -FilePath $OutputFile -Encoding UTF8
            Write-Information "Tree dump saved to $OutputFile" -InformationAction Continue
        }
        else {
            $treeOutput
        }
    }
    finally {
        # Clean up: close the app gracefully, fallback to kill
        try {
            if ($app) { $app.Close() }
        }
        catch {
            if ($process) { $process.Kill() }
        }
        if ($automation) { $automation.Dispose() }
    }
}

# Run the script's main action only when executed directly (not dot-sourced)
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-DumpUiTree -Timeout $Timeout -OutputFile $OutputFile -InstallDependencies:$InstallDependencies -FlaUIVersion $FlaUIVersion
}
