#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Clean, restore, and build WinUI project with full diagnostics.

.DESCRIPTION
    Implements the complete fix for WinUI XAML compiler errors:
    1. Removes all build artifacts
    2. Restores packages with corrected references
    3. Builds with detailed diagnostics
    4. Extracts and displays XAML compiler errors

.PARAMETER SkipClean
    Skip the git clean step (use if you have uncommitted changes)

.PARAMETER VerbosityLevel
    MSBuild verbosity level (quiet, minimal, normal, detailed, diagnostic)

.EXAMPLE
    .\winui-fix-and-build.ps1
    
.EXAMPLE
    .\winui-fix-and-build.ps1 -SkipClean -VerbosityLevel detailed

.NOTES
    Requires: PowerShell 7.5.4+, .NET 9 SDK
    Compliant with: PSScriptAnalyzer, PowerShell 7.5.4 best practices
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [switch]$SkipClean,
    
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$VerbosityLevel = 'detailed'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'
$InformationPreference = 'Continue'

# Initialize color variables using $PSStyle (PowerShell 7.5.4)
$script:ColorCyan = $PSStyle.Foreground.Cyan
$script:ColorGreen = $PSStyle.Foreground.Green
$script:ColorYellow = $PSStyle.Foreground.Yellow
$script:ColorRed = $PSStyle.Foreground.Red
$script:ColorGray = $PSStyle.Foreground.BrightBlack
$script:ColorReset = $PSStyle.Reset

# Helper function for styled information output
function Write-StyledInfo {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [Parameter()]
        [string]$Color = $script:ColorReset
    )
    
    Write-Information "${Color}${Message}${script:ColorReset}"
}

# Helper function for section headers
function Write-SectionHeader {
    param(
        [Parameter(Mandatory)]
        [string]$Title,
        
        [Parameter()]
        [string]$Icon = '📋'
    )
    
    Write-StyledInfo -Message "`n$Icon $Title" -Color $script:ColorCyan
    Write-StyledInfo -Message ('=' * ($Title.Length + 3)) -Color $script:ColorCyan
}

# Main script execution
try {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $buildLogPath = Join-Path -Path $repoRoot -ChildPath 'build-winui-diagnostic.log'
    $xamlErrorsPath = Join-Path -Path $repoRoot -ChildPath 'xaml-errors.log'

    Write-SectionHeader -Title 'WinUI Build Fix & Diagnostic' -Icon '🔧'

    # Step 1: Clean build surface
    if (-not $SkipClean) {
        Write-SectionHeader -Title 'Step 1: Cleaning build surface' -Icon '📦'
        
        Push-Location -Path $repoRoot
        try {
            Write-StyledInfo -Message '   Running: Cleaning bin/, obj/, and binlog files' -Color $script:ColorGray
            
            # Clean specific directories to avoid removing .git
            $cleanDirs = @(
                'bin', 'obj', 'MSBuild_Logs',
                'src/**/bin', 'src/**/obj',
                'test/**/bin', 'test/**/obj'
            )
            
            foreach ($dir in $cleanDirs) {
                $fullPath = Join-Path -Path $repoRoot -ChildPath $dir
                if (Test-Path -Path $fullPath) {
                    Remove-Item -Path $fullPath -Recurse -Force -ErrorAction SilentlyContinue
                    Write-StyledInfo -Message "   ✓ Removed: $dir" -Color $script:ColorGreen
                }
            }
            
            # Remove binlog files
            Get-ChildItem -Path $repoRoot -Filter '*.binlog' -Recurse -ErrorAction SilentlyContinue |
                Remove-Item -Force -ErrorAction SilentlyContinue
            
            Write-StyledInfo -Message '   ✓ Build surface cleaned' -Color $script:ColorGreen
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-StyledInfo -Message "`n⏭️  Step 1: Skipping clean (SkipClean specified)" -Color $script:ColorGray
    }

    # Step 2: Restore packages
    Write-SectionHeader -Title 'Step 2: Restoring packages' -Icon '📥'
    
    Push-Location -Path $repoRoot
    try {
        Write-Verbose -Message 'Executing: dotnet restore'
        
        $restoreOutput = & dotnet restore 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-StyledInfo -Message '   ✓ Package restore succeeded' -Color $script:ColorGreen
        }
        else {
            Write-Error -Message 'Package restore failed'
            $restoreOutput | Write-Output
            exit 1
        }
    }
    finally {
        Pop-Location
    }

    # Step 3: Build with diagnostics
    Write-SectionHeader -Title 'Step 3: Building with diagnostics' -Icon '🔨'
    Write-StyledInfo -Message "   Verbosity: $VerbosityLevel" -Color $script:ColorGray
    Write-StyledInfo -Message "   Log file: $buildLogPath" -Color $script:ColorGray

    Push-Location -Path $repoRoot
    try {
        Write-Verbose -Message "Executing: dotnet build -c Debug -v:$VerbosityLevel"
        
        $buildArgs = @(
            'build'
            'WileyWidget.sln'
            '-c', 'Debug'
            "-v:$VerbosityLevel"
            '--no-restore'
        )
        
        & dotnet $buildArgs > $buildLogPath 2>&1
        $buildExitCode = $LASTEXITCODE
        
        $statusColor = $buildExitCode -eq 0 ? $script:ColorGreen : $script:ColorRed
        Write-StyledInfo -Message "`n📊 Build completed with exit code: $buildExitCode" -Color $statusColor
    }
    finally {
        Pop-Location
    }

    # Step 4: Analyze XAML compiler output
    Write-SectionHeader -Title 'Step 4: Analyzing XAML compiler output' -Icon '🔍'

    if (Test-Path -Path $buildLogPath) {
        $xamlErrors = Select-String -Path $buildLogPath -Pattern '(XamlCompiler|error MSB3073|error XDG)' -Context 2
        
        if ($xamlErrors) {
            Write-Warning -Message 'XAML Compiler Errors Found:'
            Write-StyledInfo -Message ('=' * 40) -Color $script:ColorRed
            
            foreach ($xamlError in $xamlErrors) {
                Write-StyledInfo -Message $xamlError.Line -Color $script:ColorYellow
            }
            
            # Save to dedicated error log
            $xamlErrors | Out-File -FilePath $xamlErrorsPath -Force
            Write-StyledInfo -Message "`n   Error details saved to: $xamlErrorsPath" -Color $script:ColorGray
        }
        else {
            Write-StyledInfo -Message '   ✓ No XAML compiler errors detected' -Color $script:ColorGreen
        }
        
        # Check for package resolution warnings
        $packageWarnings = Select-String -Path $buildLogPath -Pattern "Could not resolve this reference|Assembly '.*' was not found"
        
        if ($packageWarnings) {
            Write-Warning -Message 'Package Resolution Warnings Found:'
            Write-StyledInfo -Message ('=' * 40) -Color $script:ColorYellow
            
            $packageWarnings | Select-Object -First 10 | ForEach-Object {
                Write-StyledInfo -Message $_.Line -Color $script:ColorGray
            }
            
            if ($packageWarnings.Count -gt 10) {
                Write-StyledInfo -Message "   ... and $($packageWarnings.Count - 10) more warnings" -Color $script:ColorGray
            }
        }
    }
    else {
        Write-Error -Message "Build log not found at: $buildLogPath"
    }

    # Step 5: Display summary
    Write-SectionHeader -Title 'Summary' -Icon '📋'
    Write-Output "Build log: $buildLogPath"

    if ($buildExitCode -eq 0) {
        Write-StyledInfo -Message '✅ Build SUCCEEDED' -Color $script:ColorGreen
        
        # Find the output executable
        $exePath = Get-ChildItem -Path $repoRoot -Recurse -Filter 'WileyWidget.WinUI.exe' -ErrorAction SilentlyContinue | 
                   Select-Object -First 1 -ExpandProperty FullName
        
        if ($exePath) {
            Write-Output "Executable: $exePath"
            Write-StyledInfo -Message "`n💡 To run: & '$exePath'" -Color $script:ColorCyan
        }
    }
    else {
        Write-StyledInfo -Message "❌ Build FAILED (exit code: $buildExitCode)" -Color $script:ColorRed
        Write-StyledInfo -Message "`n💡 Next steps:" -Color $script:ColorCyan
        Write-StyledInfo -Message "   1. Review XAML errors in: $xamlErrorsPath" -Color $script:ColorGray
        Write-StyledInfo -Message "   2. Check full build log: $buildLogPath" -Color $script:ColorGray
        Write-StyledInfo -Message "   3. Search for 'XamlCompiler error' in the log" -Color $script:ColorGray
    }

    exit $buildExitCode
}
catch {
    Write-Error -Message "Unexpected error: $_"
    Write-Error -ErrorRecord $_
    exit 1
}
