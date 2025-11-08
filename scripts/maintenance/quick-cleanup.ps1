# Quick Profile Cleanup Script
# Execute this to safely clean up unused PowerShell profiles

Write-Host "🧹 Quick PowerShell Profile Cleanup" -ForegroundColor Cyan
Write-Host "=" * 35 -ForegroundColor Cyan

# Define the profile to delete
$profileToDelete = "$env:USERPROFILE\OneDrive\Documents\PowerShell\profile.ps1"
$backupPath = "$profileToDelete.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "Target profile: $profileToDelete" -ForegroundColor Yellow

if (Test-Path $profileToDelete) {
    Write-Host "`n📦 Step 1: Creating backup..." -ForegroundColor Green
    Copy-Item $profileToDelete $backupPath
    Write-Host "   ✅ Backup created: $backupPath" -ForegroundColor Green

    Write-Host "`n🗑️  Step 2: Deleting unused profile..." -ForegroundColor Yellow
    Remove-Item $profileToDelete -Force
    Write-Host "   ✅ Deleted: profile.ps1" -ForegroundColor Green

    Write-Host "`n📊 Cleanup Summary:" -ForegroundColor Cyan
    Write-Host "   • Removed: CurrentUserAllHosts profile" -ForegroundColor White
    Write-Host "   • Kept: AllUsersAllHosts (global)" -ForegroundColor White
    Write-Host "   • Kept: CurrentUserCurrentHost (active)" -ForegroundColor White
    Write-Host "   • Backup available for recovery" -ForegroundColor Green

    Write-Host "`n⚠️  Next Steps:" -ForegroundColor Yellow
    Write-Host "   1. Restart PowerShell" -ForegroundColor White
    Write-Host "   2. Test that everything still works" -ForegroundColor White
    Write-Host "   3. Delete backup file if satisfied" -ForegroundColor White

}
else {
    Write-Host "❌ Profile not found: $profileToDelete" -ForegroundColor Red
    Write-Host "   (Already clean or path incorrect)" -ForegroundColor Gray
}

Write-Host "`n✨ Cleanup complete!" -ForegroundColor Green
