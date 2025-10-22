# PowerShell Profile Analysis Script
# This script analyzes PowerShell 7.5.4 profile configuration

Write-Host "=== PowerShell 7.5.4 Profile Analysis ===" -ForegroundColor Cyan
Write-Host "PSVersion: $($PSVersionTable.PSVersion)" -ForegroundColor Yellow
Write-Host "PSEdition: $($PSVersionTable.PSEdition)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Profile Loading Order (Microsoft Documentation):" -ForegroundColor Green
Write-Host "1. `$PROFILE.AllUsersAllHosts` - Affects all users, all hosts" -ForegroundColor White
Write-Host "2. `$PROFILE.AllUsersCurrentHost` - Affects all users, current host" -ForegroundColor White
Write-Host "3. `$PROFILE.CurrentUserAllHosts` - Current user, all hosts" -ForegroundColor White
Write-Host "4. `$PROFILE.CurrentUserCurrentHost` - Current user, current host" -ForegroundColor White
Write-Host ""

$profilePaths = @(
    [PSCustomObject]@{ Name = "AllUsersAllHosts"; Path = $PROFILE.AllUsersAllHosts; Order = 1 },
    [PSCustomObject]@{ Name = "AllUsersCurrentHost"; Path = $PROFILE.AllUsersCurrentHost; Order = 2 },
    [PSCustomObject]@{ Name = "CurrentUserAllHosts"; Path = $PROFILE.CurrentUserAllHosts; Order = 3 },
    [PSCustomObject]@{ Name = "CurrentUserCurrentHost"; Path = $PROFILE.CurrentUserCurrentHost; Order = 4 }
)

Write-Host "Profile Status:" -ForegroundColor Green
Write-Host ("{0,-25} {1,-8} {2,-12} {3,-20}" -f "Profile Type", "Exists", "Size", "Last Modified")
Write-Host ("-" * 80)

foreach ($profileItem in $profilePaths) {
    $exists = Test-Path $profileItem.Path
    $status = if ($exists) { "Yes" } else { "No" }

    if ($exists) {
        try {
            $fileInfo = Get-Item $profileItem.Path -ErrorAction Stop
            $size = "$($fileInfo.Length) bytes"
            $lastModified = $fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        }
        catch {
            $size = "Error"
            $lastModified = "Error"
        }
    }
    else {
        $size = "N/A"
        $lastModified = "N/A"
    }

    Write-Host ("{0,-25} {1,-8} {2,-12} {3,-20}" -f $profileItem.Name, $status, $size, $lastModified)
}

Write-Host ""
Write-Host "Microsoft PowerShell 7.5.4 Profile Best Practices:" -ForegroundColor Green
Write-Host "• Use `$PROFILE.CurrentUserAllHosts` for cross-host functions" -ForegroundColor White
Write-Host "• Use `$PROFILE.CurrentUserCurrentHost` for host-specific settings" -ForegroundColor White
Write-Host "• Avoid `$PROFILE.AllUsers*` unless system-wide configuration needed" -ForegroundColor White
Write-Host "• Test profile loading with: powershell -NoProfile" -ForegroundColor White
Write-Host "• Use `$PSScriptRoot` for relative paths in profile scripts" -ForegroundColor White
Write-Host "• Handle errors gracefully with try/catch in profiles" -ForegroundColor White
Write-Host ""

Write-Host "Current Profile Loading Test:" -ForegroundColor Green
try {
    $testCommand = "Write-Host 'Profile loaded successfully' -ForegroundColor Green"
    Invoke-Expression $testCommand
}
catch {
    Write-Warning "Profile loading test failed: $($_.Exception.Message)"
}
