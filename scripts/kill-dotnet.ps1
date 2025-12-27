#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
Verification Required Before Running:
1. Analyze: Invoke-ScriptAnalyzer -Path scripts/maintenance/kill-dotnet.ps1 -Settings .vscode/PSScriptAnalyzerSettings.psd1
2. Dry Run: pwsh scripts/kill-dotnet.ps1 -DryRun
3. Targeted Kill: pwsh scripts/kill-dotnet.ps1 -Force -Project "WileyWidget.Services.Tests"
#4. Confirm no unintended processes terminated.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$DryRun,
    [switch]$Force,
    [string]$Project = '',
    [int]$AgeMinutes = 60,
    [switch]$VerboseOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Default behavior: dry-run unless -Force is provided or the caller explicitly set -DryRun
if (-not $PSBoundParameters.ContainsKey('DryRun') -and -not $Force) {
    $DryRun = $true
}
if ($Force) { $DryRun = $false }

function Find-WorkspaceRoot {
    $current = $PSScriptRoot
    while ($current) {
        if (Test-Path (Join-Path $current 'WileyWidget.sln')) { return $current }
        try {
            $parent = [System.IO.Directory]::GetParent($current)
        } catch {
            $parent = $null
            $err = $_.Exception.Message
            Write-Verbose "Find-WorkspaceRoot: failed to get parent of '$current' - $err"
        }
        if ($null -eq $parent) { break }
        $current = $parent.FullName
    }
    return $null
}

if (-not ([type]::GetType('System.Management.ManagementDateTimeConverter', $false))) {
    try {
        Add-Type -AssemblyName System.Management -ErrorAction SilentlyContinue
    } catch {
        $err = $_.Exception.Message
        Write-Verbose "Add-Type System.Management failed: $err"
    }
}

$workspaceRoot = Find-WorkspaceRoot
if ($VerboseOutput) { Write-Verbose "Workspace root: $workspaceRoot" }

try {
    $all = Get-CimInstance Win32_Process -ErrorAction Stop
} catch {
    $err = $_.Exception.Message
    Write-Warning "Unable to enumerate processes via CIM: $err"
    exit 1
}

# Allowed parent process names that generally indicate an interactive/IDE host
$allowedParents = @('explorer.exe','powershell.exe','pwsh.exe','cmd.exe','conhost.exe','devenv.exe','Code.exe','code.exe','sihost.exe','services.exe')

$candidates = $all | Where-Object {
    $name = $_.Name
    $cmd = $_.CommandLine
    $exe = $_.ExecutablePath
    if ($name -and ($name -match 'dotnet' -or $name -match 'InfinityFlow' -or $name -match '.*Host.*')) { return $true }
    return $false
}

$results = foreach ($p in $candidates) {
    $parent = $all | Where-Object { $_.ProcessId -eq $p.ParentProcessId }
    $parentName = if ($parent) { $parent.Name } else { $null }
    $creationAgeMinutes = $null
    try {
        if ($p.CreationDate) {
            $created = [Management.ManagementDateTimeConverter]::ToDateTime($p.CreationDate)
            $creationAgeMinutes = ([DateTime]::UtcNow - $created.ToUniversalTime()).TotalMinutes
        }
    } catch {
        $err = $_.Exception.Message
        Write-Verbose "Failed to parse CreationDate for PID $($p.ProcessId): $err"
    }

    $isParentAllowed = $false
    if ($parentName) { $isParentAllowed = $allowedParents -contains $parentName }

    $isOrphan = (-not $parent) -or (-not $isParentAllowed)
    $relatedToWorkspace = $false
    if ($workspaceRoot) {
        if ($p.ExecutablePath -and $p.ExecutablePath -like "$workspaceRoot*") { $relatedToWorkspace = $true }
        if ($p.CommandLine -and $p.CommandLine -match [regex]::Escape($workspaceRoot)) { $relatedToWorkspace = $true }
    }
    if ($Project) {
        if ($p.CommandLine -and $p.CommandLine -match [regex]::Escape($Project)) { $relatedToWorkspace = $true }
        if ($p.ExecutablePath -and $p.ExecutablePath -match [regex]::Escape($Project)) { $relatedToWorkspace = $true }
    }

    [PSCustomObject]@{
        ProcessId = $p.ProcessId
        Name = $p.Name
        ExecutablePath = $p.ExecutablePath
        CommandLine = $p.CommandLine
        ParentProcessId = $p.ParentProcessId
        ParentName = $parentName
        AgeMinutes = if ($creationAgeMinutes) { [int]$creationAgeMinutes } else { $null }
        IsOrphan = $isOrphan
        RelatedToWorkspace = $relatedToWorkspace
    }
}

$toKill = $results | Where-Object { $_.RelatedToWorkspace -or ($_.IsOrphan -and $_.AgeMinutes -ne $null -and $_.AgeMinutes -gt $AgeMinutes) } | Sort-Object -Property AgeMinutes -Descending

if (-not $toKill) {
    Write-Output "No candidate orphaned .NET processes found matching criteria."
    if ($DryRun) {
        Write-Output "DryRun mode: run with -Force to actually stop processes."
    }
    exit 0
}

Write-Output "Candidate processes to stop (listing):"
$toKill | Select-Object ProcessId, Name, ParentProcessId, ParentName, AgeMinutes, ExecutablePath | Format-Table -AutoSize

if ($DryRun) {
    Write-Output "`nDryRun: no processes will be stopped. Re-run with -Force to terminate matching processes."
    exit 0
}

# Now actually stop processes (honors -WhatIf / -Confirm via ShouldProcess)
foreach ($item in $toKill) {
    $targetPid = $item.ProcessId
    $display = "PID $targetPid ($($item.Name))"
    if ($PSCmdlet.ShouldProcess($display, 'Stop process')) {
        try {
            Stop-Process -Id $targetPid -Force -ErrorAction Stop
            Write-Output ("Stopped " + $display)
        } catch {
            $err = $_.Exception.Message
            Write-Warning ("Failed to stop " + $display + ": " + $err)
        }
    }
}

Write-Output "Completed stopping candidate processes."
