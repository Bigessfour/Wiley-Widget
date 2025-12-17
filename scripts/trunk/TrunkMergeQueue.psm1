<#
.SYNOPSIS
    PowerShell module for interacting with Trunk Merge Queue via CLI.

.DESCRIPTION
    Provides cmdlets to query and manage Trunk merge queue operations using
    the trunk CLI. Supports submitting PRs, checking status, canceling, and
    pausing/resuming the queue.

.NOTES
    Requires trunk CLI >= 1.25.0 installed and authenticated (trunk login).
    To install locally, run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS).
#>

#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Checks if trunk CLI is installed and returns version info.
#>
function Test-TrunkCli {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    try {
        $version = trunk --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            return @{
                Installed = $false
                Version = $null
                Error = "trunk CLI not found or returned error"
            }
        }

        return @{
            Installed = $true
            Version = $version.Trim()
            Error = $null
        }
    }
    catch {
        return @{
            Installed = $false
            Version = $null
            Error = $_.Exception.Message
        }
    }
}

<#
.SYNOPSIS
    Gets the status of the merge queue or a specific PR.

.PARAMETER PrNumber
    Optional PR number to check. If omitted, returns overall queue status.

.PARAMETER Verbose
    Show detailed output.

.EXAMPLE
    Get-TrunkMergeQueueStatus
    # Returns overall queue status

.EXAMPLE
    Get-TrunkMergeQueueStatus -PrNumber 123
    # Returns status for PR #123
#>
function Get-TrunkMergeQueueStatus {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [int]$PrNumber,

        [Parameter(Mandatory = $false)]
        [switch]$VerboseOutput
    )

    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        throw "trunk CLI not installed or not in PATH. Run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS) to install the CLI. Error: $($cliCheck.Error)"
    }

    $trunkArgs = @('merge', 'status')
    if ($PrNumber) {
        $trunkArgs += $PrNumber.ToString()
    }
    if ($VerboseOutput) {
        $trunkArgs += '--verbose'
    }

    Write-Verbose "Running: trunk $($trunkArgs -join ' ')"

    try {
        $output = & trunk @trunkArgs 2>&1
        $exitCode = $LASTEXITCODE

        return [PSCustomObject]@{
            Success = ($exitCode -eq 0)
            ExitCode = $exitCode
            Output = $output
            PrNumber = $PrNumber
            Timestamp = Get-Date
        }
    }
    catch {
        return [PSCustomObject]@{
            Success = $false
            ExitCode = -1
            Output = $_.Exception.Message
            PrNumber = $PrNumber
            Timestamp = Get-Date
        }
    }
}

<#
.SYNOPSIS
    Submits a PR to the merge queue.

.PARAMETER PrNumber
    PR number to submit.

.PARAMETER Priority
    Queue priority (0-255, where 0 is highest). Default is normal priority.

.EXAMPLE
    Submit-TrunkMergeQueuePr -PrNumber 123
    # Submits PR #123 with normal priority

.EXAMPLE
    Submit-TrunkMergeQueuePr -PrNumber 456 -Priority 0
    # Submits PR #456 with highest priority (skip the line)
#>
function Submit-TrunkMergeQueuePr {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true)]
        [int]$PrNumber,

        [Parameter(Mandatory = $false)]
        [ValidateRange(0, 255)]
        [int]$Priority
    )

    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        throw "trunk CLI not installed. Run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS) to install the CLI. Error: $($cliCheck.Error)"
    }

    $trunkArgs = @('merge', $PrNumber.ToString())
    if ($PSBoundParameters.ContainsKey('Priority')) {
        $trunkArgs += '--priority', $Priority.ToString()
    }

    if ($PSCmdlet.ShouldProcess("PR #$PrNumber", "Submit to merge queue")) {
        Write-Verbose "Running: trunk $($trunkArgs -join ' ')"

        try {
            $output = & trunk @trunkArgs 2>&1
            $exitCode = $LASTEXITCODE

            return [PSCustomObject]@{
                Success = ($exitCode -eq 0)
                ExitCode = $exitCode
                Output = $output
                PrNumber = $PrNumber
                Priority = $Priority
                Timestamp = Get-Date
            }
        }
        catch {
            return [PSCustomObject]@{
                Success = $false
                ExitCode = -1
                Output = $_.Exception.Message
                PrNumber = $PrNumber
                Priority = $Priority
                Timestamp = Get-Date
            }
        }
    }
}

<#
.SYNOPSIS
    Cancels a PR from the merge queue.

.PARAMETER PrNumber
    PR number to cancel.

.EXAMPLE
    Remove-TrunkMergeQueuePr -PrNumber 123
    # Removes PR #123 from the queue
#>
function Remove-TrunkMergeQueuePr {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true)]
        [int]$PrNumber
    )

    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        throw "trunk CLI not installed. Run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS) to install the CLI. Error: $($cliCheck.Error)"
    }

    $trunkArgs = @('merge', 'cancel', $PrNumber.ToString())

    if ($PSCmdlet.ShouldProcess("PR #$PrNumber", "Cancel from merge queue")) {
        Write-Verbose "Running: trunk $($trunkArgs -join ' ')"

        try {
            $output = & trunk @trunkArgs 2>&1
            $exitCode = $LASTEXITCODE

            return [PSCustomObject]@{
                Success = ($exitCode -eq 0)
                ExitCode = $exitCode
                Output = $output
                PrNumber = $PrNumber
                Timestamp = Get-Date
            }
        }
        catch {
            return [PSCustomObject]@{
                Success = $false
                ExitCode = -1
                Output = $_.Exception.Message
                PrNumber = $PrNumber
                Timestamp = Get-Date
            }
        }
    }
}

<#
.SYNOPSIS
    Pauses the merge queue (admin only).

.EXAMPLE
    Suspend-TrunkMergeQueue
    # Pauses the merge queue
#>
function Suspend-TrunkMergeQueue {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param()

    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        throw "trunk CLI not installed. Run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS) to install the CLI. Error: $($cliCheck.Error)"
    }

    if ($PSCmdlet.ShouldProcess("Merge Queue", "Pause")) {
        Write-Verbose "Running: trunk merge pause"

        try {
            $output = & trunk merge pause 2>&1
            $exitCode = $LASTEXITCODE

            return [PSCustomObject]@{
                Success = ($exitCode -eq 0)
                ExitCode = $exitCode
                Output = $output
                Action = 'Pause'
                Timestamp = Get-Date
            }
        }
        catch {
            return [PSCustomObject]@{
                Success = $false
                ExitCode = -1
                Output = $_.Exception.Message
                Action = 'Pause'
                Timestamp = Get-Date
            }
        }
    }
}

<#
.SYNOPSIS
    Resumes the merge queue (admin only).

.EXAMPLE
    Resume-TrunkMergeQueue
    # Resumes the merge queue
#>
function Resume-TrunkMergeQueue {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param()

    $cliCheck = Test-TrunkCli
    if (-not $cliCheck.Installed) {
        throw "trunk CLI not installed. Run 'pwsh scripts/trunk/setup-trunk.ps1' (Windows) or 'bash scripts/trunk/setup-trunk.sh' (Linux/macOS) to install the CLI. Error: $($cliCheck.Error)"
    }

    if ($PSCmdlet.ShouldProcess("Merge Queue", "Resume")) {
        Write-Verbose "Running: trunk merge resume"

        try {
            $output = & trunk merge resume 2>&1
            $exitCode = $LASTEXITCODE

            return [PSCustomObject]@{
                Success = ($exitCode -eq 0)
                ExitCode = $exitCode
                Output = $output
                Action = 'Resume'
                Timestamp = Get-Date
            }
        }
        catch {
            return [PSCustomObject]@{
                Success = $false
                ExitCode = -1
                Output = $_.Exception.Message
                Action = 'Resume'
                Timestamp = Get-Date
            }
        }
    }
}

# Export module members
Export-ModuleMember -Function @(
    'Test-TrunkCli',
    'Get-TrunkMergeQueueStatus',
    'Submit-TrunkMergeQueuePr',
    'Remove-TrunkMergeQueuePr',
    'Suspend-TrunkMergeQueue',
    'Resume-TrunkMergeQueue'
)
