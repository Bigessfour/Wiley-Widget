# PowerShell Auto-Format Script for Wiley Widget
# This script automatically formats all PowerShell files using PSScriptAnalyzer

param(
    [switch]$Preview,
    [switch]$Force,
    [string[]]$Files,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

function Show-Help {
    Write-Output "PowerShell Auto-Format Script"
    Write-Output "============================"
    Write-Output ""
    Write-Output "Usage: .\format-powershell.ps1 [options]"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -Preview    Show what would be changed without applying changes"
    Write-Output "  -Force      Apply formatting without confirmation"
    Write-Output "  -Files      Specify specific files to format (comma-separated)"
    Write-Output "  -Help       Show this help message"
    Write-Output ""
    Write-Output "Examples:"
    Write-Output "  .\format-powershell.ps1 -Preview"
    Write-Output "  .\format-powershell.ps1 -Force"
    Write-Output "  .\format-powershell.ps1 -Files 'script1.ps1,script2.ps1'"
}

function Get-PowerShellFile {
    if ($Files) {
        $fileList = @()
        foreach ($file in $Files) {
            if (Test-Path $file) {
                $fileList += Get-Item $file
            }
            else {
                Write-Warning "File not found: $file"
            }
        }
        return $fileList
    }
    else {
        return Get-ChildItem -Path $PSScriptRoot -Filter "*.ps1" -Recurse |
               Where-Object { $_.FullName -notlike "*\bin\*" -and
                            $_.FullName -notlike "*\obj\*" -and
                            $_.FullName -notlike "*\TestResults\*" }
    }
}

function Format-PowerShellFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File,
        [switch]$Preview
    )

    Write-Verbose "Processing: $($File.Name)"

    try {
        $originalContent = Get-Content $File.FullName -Raw

        if ($Preview) {
            # Show what would be changed
            $formattedContent = Invoke-Formatter -ScriptDefinition $originalContent -Settings @{
                IncludeRules = @('PSPlaceOpenBrace', 'PSPlaceCloseBrace', 'PSUseConsistentIndentation', 'PSAlignAssignmentStatement')
                Rules        = @{
                    PSPlaceOpenBrace           = @{ Enable = $true; OnSameLine = $true; NewLineAfter = $true }
                    PSPlaceCloseBrace          = @{ Enable = $true; NewLineAfter = $true }
                    PSUseConsistentIndentation = @{ Enable = $true; IndentationSize = 4 }
                    PSAlignAssignmentStatement = @{ Enable = $true }
                }
            }

            if ($originalContent -ne $formattedContent) {
                Write-Information "  📝 Would format: $($File.Name)" -InformationAction Continue
                return $true
            }
            else {
                Write-Verbose "  ✅ Already formatted: $($File.Name)"
                return $false
            }
        }
        else {
            # Apply formatting
            $formattedContent = Invoke-Formatter -ScriptDefinition $originalContent -Settings @{
                IncludeRules = @('PSPlaceOpenBrace', 'PSPlaceCloseBrace', 'PSUseConsistentIndentation', 'PSAlignAssignmentStatement')
                Rules        = @{
                    PSPlaceOpenBrace           = @{ Enable = $true; OnSameLine = $true; NewLineAfter = $true }
                    PSPlaceCloseBrace          = @{ Enable = $true; NewLineAfter = $true }
                    PSUseConsistentIndentation = @{ Enable = $true; IndentationSize = 4 }
                    PSAlignAssignmentStatement = @{ Enable = $true }
                }
            }

            if ($originalContent -ne $formattedContent) {
                # Create backup
                $backupPath = "$($File.FullName).backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
                Copy-Item $File.FullName $backupPath -Force

                # Apply formatting
                $formattedContent | Out-File $File.FullName -Encoding UTF8 -Force -NoNewline

                Write-Information "  ✅ Formatted: $($File.Name)" -InformationAction Continue
                Write-Verbose "  📋 Backup created: $(Split-Path $backupPath -Leaf)"
                return $true
            }
            else {
                Write-Verbose "  ✅ Already formatted: $($File.Name)"
                return $false
            }
        }
    }
    catch {
        Write-Error "  ❌ Error formatting $($File.Name): $($_.Exception.Message)"
        return $false
    }
}

# Main execution
if ($Help) {
    Show-Help
    exit 0
}

Write-Information "🔧 PowerShell Auto-Format Tool" -InformationAction Continue
Write-Information "=============================" -InformationAction Continue

# Check if PSScriptAnalyzer is available
try {
    Import-Module PSScriptAnalyzer -ErrorAction Stop
    Write-Information "✅ PSScriptAnalyzer module loaded" -InformationAction Continue
}
catch {
    Write-Error "❌ PSScriptAnalyzer module not found. Install with: Install-Module PSScriptAnalyzer"
    exit 1
}

# Get PowerShell files
$psFiles = Get-PowerShellFiles

if ($psFiles.Count -eq 0) {
    Write-Warning "No PowerShell files found to format."
    exit 0
}

Write-Information "Found $($psFiles.Count) PowerShell files" -InformationAction Continue

if ($Preview) {
    Write-Information "🔍 Preview mode - showing what would be changed..." -InformationAction Continue
}
elseif (-not $Force) {
    $response = Read-Host "This will format $($psFiles.Count) PowerShell files. Continue? (y/N)"
    if ($response -notmatch "^[Yy]") {
        Write-Warning "Operation cancelled."
        exit 0
    }
}

$formattedCount = 0
$totalFiles = $psFiles.Count

foreach ($file in $psFiles) {
    if (Format-PowerShellFile -File $file -Preview:$Preview) {
        $formattedCount++
    }
}

Write-Information "" -InformationAction Continue
if ($Preview) {
    Write-Information "📊 Preview complete: $formattedCount of $totalFiles files would be formatted" -InformationAction Continue
}
else {
    Write-Information "🎉 Formatting complete: $formattedCount of $totalFiles files were formatted" -InformationAction Continue
    if ($formattedCount -gt 0) {
        Write-Information "💡 Backups were created for all modified files" -InformationAction Continue
    }
}
