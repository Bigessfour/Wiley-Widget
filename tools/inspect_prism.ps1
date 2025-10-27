<#
inspect_prism.ps1

Small inspector for Prism.Wpf assembly attributes and exported types.
Uses standard PowerShell cmdlet patterns so it passes PSScriptAnalyzer rules
and can be used in automation without Write-Host usage.

Usage:
  pwsh -NoProfile -File .\tools\inspect_prism.ps1 -Path <path-to-Prism.Wpf.dll>
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]
    $Path = 'C:\Users\biges\.nuget\packages\prism.wpf\9.0.537\lib\net6.0-windows7.0\Prism.Wpf.dll'
)

if (-not (Test-Path -Path $Path)) {
    Write-Error "File not found: $Path"
    exit 1
}

try {
    $assembly = [Reflection.Assembly]::LoadFrom($Path)
    Write-Output "Loaded assembly: $($assembly.FullName)"
    Write-Output "Exported types that match 'PrismApplication' or 'Prism':"
    $assembly.GetExportedTypes() |
        Where-Object { $_.Name -like '*Prism*' -or $_.Name -like '*PrismApplication*' } |
        ForEach-Object { Write-Output $_.FullName }

    Write-Output "Assembly custom attributes:"
    [Attribute]::GetCustomAttributes($assembly) |
        ForEach-Object { Write-Output ($_.GetType().FullName + ' -> ' + $_.ToString()) }

    exit 0
} catch {
    Write-Error "Error inspecting assembly: $($_.Exception.Message)"
    Write-Verbose "Exception details: $($_ | Out-String)"
    exit 1
}
