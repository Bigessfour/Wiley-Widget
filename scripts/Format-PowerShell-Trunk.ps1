# PowerShell 7.5.2 Formatter for Trunk CI/CD
# This script ensures all PowerShell files follow PowerShell 7.5.2 formatting standards

param(
    [Parameter(Mandatory = $false)]
    [string[]]$Path = @("scripts", "src"),

    [Parameter(Mandatory = $false)]
    [switch]$CheckOnly,

    [Parameter(Mandatory = $false)]
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

# PowerShell 7.5.2 formatting rules
$FormattingRules = @{
    # Brace placement
    PlaceOpenBrace = @{
        Enable = $true
        OnSameLine = $true
        NewLineAfter = $true
    }

    # Indentation
    ConsistentIndentation = @{
        Enable = $true
        IndentationSize = 4
        Kind = 'space'
    }

    # Whitespace
    ConsistentWhitespace = @{
        Enable = $true
        CheckInnerBrace = $true
        CheckOpenBrace = $true
        CheckOpenParen = $true
        CheckOperator = $true
        CheckPipe = $true
        CheckSeparator = $true
    }

    # Assignment alignment
    AlignAssignment = @{
        Enable = $true
        CheckHashtable = $true
    }
}

function Test-PowerShellFormatting {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $violations = @()
    $content = Get-Content $File.FullName -Raw

    # Check for common formatting issues

    # 1. Check brace placement
    $openBracePattern = '(?<!\s){(?!\s)'
    if ($content -match $openBracePattern) {
        $violations += @{
            Rule = "PSPlaceOpenBrace"
            Message = "Open brace should be preceded by space and followed by newline"
            Severity = "Error"
        }
    }

    # 2. Check indentation (should be 4 spaces, not tabs)
    $lines = Get-Content $File.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\t') {
            $violations += @{
                Rule = "PSUseConsistentIndentation"
                Message = "Line $($i + 1): Use spaces instead of tabs for indentation"
                Severity = "Error"
            }
        }
    }

    # 3. Check for trailing whitespace
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '\s+$') {
            $violations += @{
                Rule = "PSUseConsistentWhitespace"
                Message = "Line $($i + 1): Remove trailing whitespace"
                Severity = "Warning"
            }
        }
    }

    # 4. Check for Write-Host usage
    if ($content -match 'Write-Host') {
        $violations += @{
            Rule = "PSAvoidUsingWriteHost"
            Message = "Avoid using Write-Host. Use Write-Output, Write-Information, Write-Verbose, Write-Warning, or Write-Error instead"
            Severity = "Error"
        }
    }

    return $violations
}

function Format-PowerShellFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $originalContent = Get-Content $File.FullName -Raw

    # Apply formatting using PSScriptAnalyzer if available
    if (Get-Module -Name PSScriptAnalyzer -ListAvailable) {
        try {
            $formattedContent = Invoke-Formatter -ScriptDefinition $originalContent -Settings @{
                IncludeRules = @('PSPlaceOpenBrace', 'PSPlaceCloseBrace', 'PSUseConsistentIndentation', 'PSUseConsistentWhitespace', 'PSAlignAssignmentStatement')
                Rules        = @{
                    PSPlaceOpenBrace           = @{ Enable = $true; OnSameLine = $true; NewLineAfter = $true }
                    PSPlaceCloseBrace          = @{ Enable = $true; NewLineAfter = $true }
                    PSUseConsistentIndentation = @{ Enable = $true; IndentationSize = 4 }
                    PSAlignAssignmentStatement = @{ Enable = $true }
                }
            }

            # Write formatted content back to file
            $formattedContent | Out-File $File.FullName -Encoding UTF8 -Force -NoNewline

            Write-Information "Formatted: $($File.Name)" -InformationAction Continue
            return $true
        }
        catch {
            Write-Warning "Failed to format $($File.Name): $($_.Exception.Message)"
            return $false
        }
    }
    else {
        Write-Warning "PSScriptAnalyzer not available. Install with: Install-Module PSScriptAnalyzer"
        return $false
    }
}

function Get-PowerShellFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$SearchPaths
    )

    $files = @()

    foreach ($searchPath in $SearchPaths) {
        if (Test-Path $searchPath) {
            $files += Get-ChildItem -Path $searchPath -Filter "*.ps1" -Recurse -File |
                     Where-Object {
                         $_.FullName -notlike "*\bin\*" -and
                         $_.FullName -notlike "*\obj\*" -and
                         $_.FullName -notlike "*\TestResults\*" -and
                         $_.FullName -notlike "*\.git\*"
                     }
        }
    }

    return $files
}

# Main execution
Write-Information "PowerShell 7.5.2 Formatter for Trunk CI/CD" -InformationAction Continue
Write-Information "========================================" -InformationAction Continue

# Get PowerShell files
$psFiles = Get-PowerShellFiles -SearchPaths $Path

if ($psFiles.Count -eq 0) {
    Write-Information "No PowerShell files found to process." -InformationAction Continue
    exit 0
}

Write-Information "Found $($psFiles.Count) PowerShell files" -InformationAction Continue

$totalViolations = 0
$filesWithViolations = 0

foreach ($file in $psFiles) {
    Write-Verbose "Processing: $($file.Name)"

    if ($CheckOnly) {
        # Only check for violations
        $violations = Test-PowerShellFormatting -File $file

        if ($violations.Count -gt 0) {
            Write-Warning "Violations found in $($file.Name):"
            foreach ($violation in $violations) {
                Write-Warning "  $($violation.Rule): $($violation.Message)"
            }
            $filesWithViolations++
            $totalViolations += $violations.Count
        }
    }
    elseif ($Fix) {
        # Check and fix
        $violations = Test-PowerShellFormatting -File $file

        if ($violations.Count -gt 0) {
            Write-Warning "Fixing violations in $($file.Name)..."
            $fixed = Format-PowerShellFile -File $file

            if ($fixed) {
                Write-Information "Successfully formatted: $($file.Name)" -InformationAction Continue
            }
        }
        else {
            Write-Verbose "No violations found in $($file.Name)"
        }
    }
}

# Summary
if ($CheckOnly) {
    Write-Information "" -InformationAction Continue
    if ($filesWithViolations -gt 0) {
        Write-Warning "Summary: $filesWithViolations files with $totalViolations violations found"
        exit 1  # Exit with error code for CI/CD
    }
    else {
        Write-Information "✅ All PowerShell files are properly formatted!" -InformationAction Continue
        exit 0
    }
}
elseif ($Fix) {
    Write-Information "" -InformationAction Continue
    Write-Information "🎉 PowerShell formatting complete!" -InformationAction Continue
}
