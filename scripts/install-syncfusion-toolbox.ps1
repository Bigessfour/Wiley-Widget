# Installs Syncfusion Toolbox if present. Elevation required for toolbox install.
$installer = 'C:\Program Files (x86)\Syncfusion\Essential Studio\WinUI\31.2.2\Utilities\Toolbox Installer\SyncfusionToolboxInstaller.exe'
if (Test-Path $installer) {
    Write-Host "Found toolbox installer. Attempting to run..."
    Start-Process -FilePath $installer -ArgumentList '/ide:VS2022','/winui' -Wait -Verb RunAs
    Write-Host "Toolbox installer finished."
} else {
    Write-Host "Toolbox installer not found at path: $installer" -ForegroundColor Yellow
}
