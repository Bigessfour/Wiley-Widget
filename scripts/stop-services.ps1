# Stop non-essential services for development to free up resources
# This script stops a predefined list of services that are safe to disable during development
# and do not interfere with core development tasks.

Get-Service -Name SysMain,WSearch,DiagTrack,XblGameSave,XblAuthManager,XboxNetApiSvc,XboxGipSvc,Spooler,Fax,WerSvc,PcaSvc -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Running' } | ForEach-Object {
    try {
        Stop-Service -InputObject $_ -Force -Confirm:$false
        Write-Host "Stopped $($_.Name)"
    } catch {
        Write-Warning "Failed to stop $($_.Name): $_"
    }
}
