# Production Database Automation Setup for Wiley Widget
# This script sets up automated maintenance and monitoring tasks

param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Test,
    [string]$MaintenanceTime = "02:00",  # 2:00 AM
    [string]$MonitorInterval = "15",     # Every 15 minutes
    [string]$User = $env:USERNAME
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$maintenanceScript = Join-Path $scriptPath "production-db-maintenance.ps1"
$monitorScript = Join-Path $scriptPath "production-db-monitor.ps1"

# Task names
$maintenanceTaskName = "WileyWidget-Production-DB-Maintenance"
$monitorTaskName = "WileyWidget-Production-DB-Monitor"

function Write-SetupLog {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Output "[$timestamp] [$Level] $Message"
}

function Test-ScriptPrerequisite {
    Write-SetupLog "Testing prerequisites..."

    # Check if scripts exist
    if (!(Test-Path $maintenanceScript)) {
        throw "Maintenance script not found: $maintenanceScript"
    }
    if (!(Test-Path $monitorScript)) {
        throw "Monitor script not found: $monitorScript"
    }

    # Test script execution
    try {
        & $maintenanceScript -DryRun | Out-Null
        Write-SetupLog "Maintenance script test passed"
    }
    catch {
        Write-SetupLog "Maintenance script test failed: $($_.Exception.Message)" -Level "WARNING"
    }

    try {
        & $monitorScript -JsonOutput | Out-Null
        Write-SetupLog "Monitor script test passed"
    }
    catch {
        Write-SetupLog "Monitor script test failed: $($_.Exception.Message)" -Level "WARNING"
    }

    Write-SetupLog "Prerequisites check completed"
}

function Install-ScheduledTask {
    param([string]$TaskUser)

    Write-SetupLog "Installing scheduled tasks..."

    # Create maintenance task (daily at specified time)
    $maintenanceAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File `"$maintenanceScript`" -Server `".\SQLEXPRESS`" -Database `"WileyWidgetDev`""
    $maintenanceTrigger = New-ScheduledTaskTrigger -Daily -At $MaintenanceTime
    $maintenanceSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RunOnlyIfNetworkAvailable

    Register-ScheduledTask -TaskName $maintenanceTaskName -Action $maintenanceAction -Trigger $maintenanceTrigger -Settings $maintenanceSettings -User $TaskUser -RunLevel Highest -Description "Daily production database maintenance for Wiley Widget"

    Write-SetupLog "Maintenance task installed: $maintenanceTaskName"

    # Create monitoring task (every 15 minutes)
    $monitorAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File `"$monitorScript`" -Server `".\SQLEXPRESS`" -Database `"WileyWidgetDev`" -JsonOutput"
    $monitorTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes $MonitorInterval) -RepetitionDuration (New-TimeSpan -Days 1)

    Register-ScheduledTask -TaskName $monitorTaskName -Action $monitorAction -Trigger $monitorTrigger -Settings $maintenanceSettings -User $TaskUser -RunLevel Highest -Description "Continuous production database monitoring for Wiley Widget"

    Write-SetupLog "Monitor task installed: $monitorTaskName"
}

function Uninstall-ScheduledTask {
    Write-SetupLog "Uninstalling scheduled tasks..."

    try {
        Unregister-ScheduledTask -TaskName $maintenanceTaskName -Confirm:$false
        Write-SetupLog "Maintenance task uninstalled: $maintenanceTaskName"
    }
    catch {
        Write-SetupLog "Maintenance task not found or already uninstalled" -Level "WARNING"
    }

    try {
        Unregister-ScheduledTask -TaskName $monitorTaskName -Confirm:$false
        Write-SetupLog "Monitor task uninstalled: $monitorTaskName"
    }
    catch {
        Write-SetupLog "Monitor task not found or already uninstalled" -Level "WARNING"
    }
}

function Show-TaskStatus {
    Write-SetupLog "Checking task status..."

    $tasks = @($maintenanceTaskName, $monitorTaskName)
    foreach ($taskName in $tasks) {
        try {
            $task = Get-ScheduledTask -TaskName $taskName
            Write-SetupLog "Task '$taskName': $($task.State) - Next run: $($task.NextRunTime)"
        }
        catch {
            Write-SetupLog "Task '$taskName': Not found" -Level "WARNING"
        }
    }
}

# Main execution
try {
    Write-SetupLog "Wiley Widget Production Database Automation Setup"
    Write-SetupLog "=================================================="

    if ($Test) {
        Test-ScriptPrerequisite
        Show-TaskStatus
    }
    elseif ($Install) {
        Test-ScriptPrerequisite
        Install-ScheduledTask -TaskUser $User
        Write-SetupLog "Installation completed successfully"
        Write-SetupLog "Maintenance schedule: Daily at $MaintenanceTime"
        Write-SetupLog "Monitoring interval: Every $MonitorInterval minutes"
    }
    elseif ($Uninstall) {
        Uninstall-ScheduledTask
        Write-SetupLog "Uninstallation completed"
    }
    else {
        Write-SetupLog "Usage:"
        Write-SetupLog "  .\production-db-automation.ps1 -Install    # Install scheduled tasks"
        Write-SetupLog "  .\production-db-automation.ps1 -Uninstall  # Remove scheduled tasks"
        Write-SetupLog "  .\production-db-automation.ps1 -Test       # Test setup and show status"
        Write-SetupLog ""
        Write-SetupLog "Parameters:"
        Write-SetupLog "  -MaintenanceTime 'HH:mm'  # Default: 02:00"
        Write-SetupLog "  -MonitorInterval 'minutes' # Default: 15"
        Write-SetupLog "  -User 'username'          # Default: current user"
    }

}
catch {
    Write-SetupLog "Setup failed: $($_.Exception.Message)" -Level "ERROR"
    exit 1
}
