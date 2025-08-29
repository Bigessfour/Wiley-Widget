# PowerShell Profile for Wiley Widget Development
# This profile enforces Write-Host avoidance and PowerShell 7.5.2 best practices

# Set execution policy for development
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force

# Configure PowerShell for development
$PSDefaultParameterValues = @{
    'Write-Verbose:Verbose' = $true
    'Write-Information:InformationAction' = 'Continue'
}

# Custom function to override Write-Host (prevents accidental usage)
function Write-Host {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [Parameter()]
        [System.ConsoleColor]$ForegroundColor = [System.ConsoleColor]::White,

        [Parameter()]
        [System.ConsoleColor]$BackgroundColor = [System.ConsoleColor]::Black,

        [switch]$NoNewline
    )

    # Issue warning when Write-Host is used
    Write-Warning "Write-Host detected! Use Write-Output, Write-Information, Write-Verbose, Write-Warning, or Write-Error instead."

    # Still allow the output but with warning
    $currentForeground = [Console]::ForegroundColor
    $currentBackground = [Console]::BackgroundColor

    try {
        [Console]::ForegroundColor = $ForegroundColor
        [Console]::BackgroundColor = $BackgroundColor

        if ($NoNewline) {
            [Console]::Write($Message)
        }
        else {
            [Console]::WriteLine($Message)
        }
    }
    finally {
        [Console]::ForegroundColor = $currentForeground
        [Console]::BackgroundColor = $currentBackground
    }
}

# Load PSScriptAnalyzer if available
if (Get-Module -Name PSScriptAnalyzer -ListAvailable) {
    Import-Module PSScriptAnalyzer -Force
    Write-Information "PSScriptAnalyzer loaded for code analysis" -InformationAction Continue
}

# Set aliases for common development tasks
Set-Alias -Name analyze -Value Invoke-ScriptAnalyzer
Set-Alias -Name format -Value Invoke-Formatter

# Display development environment info
Write-Information "PowerShell $($PSVersionTable.PSVersion) Development Environment Loaded" -InformationAction Continue
Write-Information "Write-Host override active - use approved cmdlets instead" -InformationAction Continue

# Function to check for Write-Host usage in scripts
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Path
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Path
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Path
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Path
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
<#
.SYNOPSIS
Short description

.DESCRIPTION
Long description

.PARAMETER Path
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
function Test-WriteHostUsage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $files = Get-ChildItem -Path $Path -Filter "*.ps1" -Recurse
    $violations = @()

    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match 'Write-Host') {
            $violations += @{
                File = $file.FullName
                Matches = ($content | Select-String 'Write-Host' -AllMatches).Matches.Count
            }
        }
    }

    if ($violations.Count -gt 0) {
        Write-Warning "Found Write-Host usage in the following files:"
        foreach ($violation in $violations) {
            Write-Warning "  $($violation.File) ($($violation.Matches) occurrences)"
        }
    }
    else {
        Write-Information "No Write-Host usage found in PowerShell files" -InformationAction Continue
    }
}

# Export functions for use in scripts
Export-ModuleMember -Function Test-WriteHostUsage
