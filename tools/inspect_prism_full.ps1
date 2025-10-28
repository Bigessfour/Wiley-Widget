<#
inspect_prism_full.ps1

Loads Prism core and Prism.Wpf assemblies (from NuGet cache) and
prints exported types and assembly attributes for Prism.Wpf. This
helps determine the correct CLR namespace or XmlnsDefinition to
use in XAML (e.g., mapping http://prismlibrary.com/).

Usage:
  pwsh -NoProfile -File .\tools\inspect_prism_full.ps1
#>

[CmdletBinding()]
param(
    [string] $PrismPath = 'C:\Users\biges\.nuget\packages\prism.core\9.0.537\lib\net6.0\Prism.dll',
    [string] $PrismWpfPath = 'C:\Users\biges\.nuget\packages\prism.wpf\9.0.537\lib\net6.0-windows7.0\Prism.Wpf.dll'
)

if (-not (Test-Path -Path $PrismPath)) { Write-Error "Prism core not found: $PrismPath"; exit 2 }
if (-not (Test-Path -Path $PrismWpfPath)) { Write-Error "Prism.Wpf not found: $PrismWpfPath"; exit 2 }

try {
    $core = [Reflection.Assembly]::LoadFrom($PrismPath)
    Write-Output "Loaded Prism core: $($core.FullName)"

    $wpf = [Reflection.Assembly]::LoadFrom($PrismWpfPath)
    Write-Output "Loaded Prism.Wpf: $($wpf.FullName)"

    Write-Output "Exported types from Prism.Wpf matching 'PrismApplication' or 'Prism':"
    $wpf.GetExportedTypes() |
        Where-Object { $_.Name -like '*Prism*' -or $_.Name -like '*PrismApplication*' } |
        ForEach-Object { Write-Output $_.FullName }

    Write-Output "XmlnsDefinition / XmlnsPrefix attributes on Prism.Wpf assembly:"
    [Attribute]::GetCustomAttributes($wpf) |
        Where-Object { $_.GetType().Name -like '*Xmlns*' } |
        ForEach-Object { Write-Output ($_.GetType().FullName + ' -> ' + $_.ToString()) }

    exit 0
}
catch {
    Write-Error "Error inspecting Prism assemblies: $($_.Exception.Message)"
    exit 1
}
