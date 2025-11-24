$zip = Join-Path $PSScriptRoot '..\..\Procdump.zip'
if (-not (Test-Path $zip)) {
    Write-Error "Procdump.zip not found at: $zip"
    exit 1
}

Write-Host "Extracting $zip to $PSScriptRoot"
Expand-Archive -LiteralPath $zip -DestinationPath $PSScriptRoot -Force

$exe = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter 'procdump*.exe' -File | Select-Object -First 1
if ($null -ne $exe) {
    Copy-Item $exe.FullName -Destination $PSScriptRoot -Force
    Write-Host "Copied: $($exe.FullName) to $PSScriptRoot"
    exit 0
}
else {
    Write-Error 'procdump.exe not found inside zip'
    exit 2
}