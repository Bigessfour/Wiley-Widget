# Scheduled Task: Ensure Syncfusion License Key Persistence
# This script runs periodically to ensure the license key remains available

param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Run,
    [int]$IntervalMinutes = 60  # Default: check every hour
)

$TaskName = "WileyWidget-Syncfusion-License-Monitor"
$ScriptPath = Join-Path $PSScriptRoot "ensure-syncfusion-license.ps1"

function Install-ScheduledTask {
    Write-Host "Installing scheduled task: $TaskName"

    # Check if task already exists
    $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "Task already exists. Removing old task first..."
        Uninstall-ScheduledTask
    }

    # Create new scheduled task
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File `"$ScriptPath`" -Process -Force"
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) -RepetitionDuration (New-TimeSpan -Days 365)
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType InteractiveToken

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Ensures Syncfusion license key is always available for Wiley Widget development"

    Write-Host "Scheduled task installed successfully"
    Write-Host "Task will run every $IntervalMinutes minutes"
}

function Uninstall-ScheduledTask {
    Write-Host "Removing scheduled task: $TaskName"

    $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Scheduled task uninstalled successfully"
    } else {
        Write-Host "Scheduled task not found"
    }
}

function Run-ManualCheck {
    Write-Host "Running manual license key check..."

    if (Test-Path $ScriptPath) {
        & $ScriptPath -Verify
    } else {
        Write-Error "License ensure script not found: $ScriptPath"
    }
}

# Main execution
if ($Install) {
    Install-ScheduledTask
}
elseif ($Uninstall) {
    Uninstall-ScheduledTask
}
elseif ($Run) {
    Run-ManualCheck
}
else {
    Write-Host "Wiley Widget Syncfusion License Monitor" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\schedule-license-monitor.ps1 -Install          # Install scheduled task"
    Write-Host "  .\schedule-license-monitor.ps1 -Uninstall        # Remove scheduled task"
    Write-Host "  .\schedule-license-monitor.ps1 -Run              # Run manual check"
    Write-Host "  .\schedule-license-monitor.ps1 -Install -IntervalMinutes 30  # Install with custom interval"
    Write-Host ""
    Write-Host "Current status:"
    $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "  Scheduled task is installed"
        Write-Host "  Next run: $($existingTask.NextRunTime)"
        Write-Host "  Interval: Every $IntervalMinutes minutes"
    } else {
        Write-Host "  Scheduled task is not installed"
    }
}
