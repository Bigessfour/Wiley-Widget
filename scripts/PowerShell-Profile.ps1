# .NET Development Process Management Functions
# Add this to your PowerShell profile ($PROFILE)

function Invoke-DotNetCleanup {
    <#
    .SYNOPSIS
        Cleans up orphaned .NET processes and build artifacts
    .DESCRIPTION
        Professional .NET development cleanup utility
    .PARAMETER Force
        Force kill processes without confirmation
    .PARAMETER Clean
        Also clean build artifacts
    .EXAMPLE
        cleanup-dotnet -Force -Clean
    #>
    param(
        [switch]$Force,
        [switch]$Clean
    )

    $scriptPath = Join-Path $PSScriptRoot "scripts\kill-dotnet.ps1"
    if (Test-Path $scriptPath) {
        $scriptArgs = @()
        if ($Force) { $scriptArgs += "-Force" }
        if ($Clean) { $scriptArgs += "-Clean" }

        & $scriptPath @scriptArgs
    }
    else {
        Write-Warning "kill-dotnet.ps1 script not found at $scriptPath"
    }
}

function Start-DotNetWatch {
    <#
    .SYNOPSIS
        Starts dotnet watch with automatic cleanup
    .DESCRIPTION
        Runs dotnet watch while ensuring no orphaned processes
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [string]$Project = "WileyWidget.csproj",
        [switch]$NoCleanup
    )

    if (-not $NoCleanup) {
        if ($PSCmdlet.ShouldProcess("Orphaned .NET processes", "Clean up")) {
            Invoke-DotNetCleanup -Force
        }
    }

    if ($PSCmdlet.ShouldProcess("dotnet watch for $Project", "Start")) {
        Write-Verbose "🚀 Starting dotnet watch..."
        dotnet watch --project $Project run
    }
}

function Get-DotNetProcess {
    <#
    .SYNOPSIS
        Lists all .NET-related processes with details
    #>
    Get-Process -Name "dotnet", "WileyWidget", "testhost", "vstest.console" -ErrorAction SilentlyContinue |
        Select-Object Name, Id, CPU, @{Name = "MemoryMB"; Expression = { [math]::Round($_.WorkingSet64 / 1MB, 2) } }, StartTime |
        Sort-Object StartTime -Descending
}

# Aliases for convenience
Set-Alias cleanup Invoke-DotNetCleanup
Set-Alias dotnet-watch Start-DotNetWatch
# Fix alias target to match the actual function name Get-DotNetProcess
Set-Alias psdotnet Get-DotNetProcess

# Auto-cleanup on profile load (optional - comment out if not wanted)
# Invoke-DotNetCleanup -Force

Write-Information "🔧 .NET Development functions loaded. Use 'cleanup' to clean orphaned processes."
