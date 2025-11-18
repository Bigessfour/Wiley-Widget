#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes XAML namespace declarations for Uno Platform compatibility

.DESCRIPTION
    Converts WPF-style XAML namespaces to WinUI/Uno compatible format:
    - Removes xmlns:prism="http://prismlibrary.com/"
    - Converts clr-namespace: to using:
    - Fixes Prism.Regions to Prism.Navigation.Regions
    - Updates Syncfusion WPF schemas to WinUI

.PARAMETER ProjectPath
    Path to the WinUI project (default: src/WileyWidget.WinUI)

.PARAMETER DryRun
    Preview changes without applying them

.EXAMPLE
    .\Fix-XamlNamespaces.ps1 -DryRun
    .\Fix-XamlNamespaces.ps1 -Verbose
#>

#Requires -Version 7.5

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = "src/WileyWidget.WinUI",
    
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Initialize colors
$script:ColorGreen = $PSStyle.Foreground.Green
$script:ColorRed = $PSStyle.Foreground.Red
$script:ColorCyan = $PSStyle.Foreground.Cyan
$script:ColorYellow = $PSStyle.Foreground.Yellow
$script:ColorReset = $PSStyle.Reset

try {
    Write-Information "${script:ColorCyan}🔧 XAML Namespace Fix Tool${script:ColorReset}`n"
    
    if ($DryRun) {
        Write-Information "${script:ColorYellow}🔍 DRY RUN MODE - No files will be modified${script:ColorReset}`n"
    }
    
    # Validate project path
    if (-not (Test-Path $ProjectPath)) {
        throw "Project path not found: $ProjectPath"
    }
    
    $xamlFiles = Get-ChildItem -Path $ProjectPath -Filter "*.xaml" -Recurse -File
    Write-Information "Found $($xamlFiles.Count) XAML files to process`n"
    
    $fixes = @()
    $totalFixes = 0
    
    foreach ($file in $xamlFiles) {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8
        $originalContent = $content
        $fileFixed = $false
        $fileFixes = @()
        
        # Fix 1: Remove xmlns:prism="http://prismlibrary.com/"
        if ($content -match 'xmlns:prism="http://prismlibrary.com/"') {
            $content = $content -replace '\s*xmlns:prism="http://prismlibrary.com/"', ''
            $fileFixed = $true
            $fileFixes += "Removed WPF Prism namespace"
        }
        
        # Fix 2: Convert clr-namespace to using
        if ($content -match 'clr-namespace:') {
            $content = $content -replace 'clr-namespace:([^;]+);assembly=([^"]+)', 'using:$1'
            $content = $content -replace 'clr-namespace:([^"]+)"', 'using:$1"'
            $fileFixed = $true
            $fileFixes += "Converted clr-namespace to using:"
        }
        
        # Fix 3: Fix Prism.Regions to Prism.Navigation.Regions
        if ($content -match 'using:Prism\.Regions[^.]') {
            $content = $content -replace 'using:Prism\.Regions([^.])', 'using:Prism.Navigation.Regions$1'
            $fileFixed = $true
            $fileFixes += "Fixed Prism namespace path"
        }
        
        # Fix 4: Remove WPF Syncfusion schemas
        if ($content -match 'http://schemas\.syncfusion\.com/wpf') {
            $content = $content -replace 'xmlns:syncfusion="http://schemas\.syncfusion\.com/wpf"', 'xmlns:syncfusion="using:Syncfusion.UI.Xaml"'
            $fileFixed = $true
            $fileFixes += "Updated Syncfusion namespace"
        }
        
        # Fix 5: Clean up multiple blank lines
        if ($fileFixed) {
            $content = $content -replace '(\r?\n){3,}', "`r`n`r`n"
        }
        
        if ($fileFixed) {
            $totalFixes += $fileFixes.Count
            $relativePath = $file.FullName.Replace($PWD.Path, '').TrimStart('\', '/')
            
            if ($DryRun) {
                Write-Information "${script:ColorYellow}🔍 Would fix: $relativePath${script:ColorReset}"
                foreach ($fix in $fileFixes) {
                    Write-Information "   - $fix"
                }
            }
            else {
                Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
                Write-Information "${script:ColorGreen}✅ Fixed: $relativePath${script:ColorReset}"
                foreach ($fix in $fileFixes) {
                    Write-Information "   - $fix"
                }
            }
            
            $fixes += [PSCustomObject]@{
                File = $relativePath
                Fixes = $fileFixes
                FixCount = $fileFixes.Count
            }
        }
    }
    
    Write-Information "`n${script:ColorCyan}📊 Summary:${script:ColorReset}"
    Write-Information "   Files processed: $($xamlFiles.Count)"
    Write-Information "   Files fixed: $($fixes.Count)"
    Write-Information "   Total fixes applied: $totalFixes"
    
    if ($DryRun) {
        Write-Information "`n${script:ColorYellow}Run without -DryRun to apply changes${script:ColorReset}"
    }
    else {
        Write-Information "`n${script:ColorGreen}✅ XAML namespace fixes complete!${script:ColorReset}"
    }
}
catch {
    Write-Error "Failed to fix XAML namespaces: $_"
    exit 1
}
