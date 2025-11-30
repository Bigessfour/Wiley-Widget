$ErrorActionPreference = 'Stop'

$pkgRoot = Join-Path $env:USERPROFILE ".nuget\packages\syncfusion.tools.windows"
$items = Get-ChildItem -Path $pkgRoot -Recurse -Filter 'Syncfusion.Tools.Windows.dll' -File -ErrorAction SilentlyContinue
if(-not $items)
{
    Write-Host "DLL not found under $pkgRoot"
    exit 1
}

$dll = ($items | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
Write-Host "Inspecting: $dll`n"

try
{
    $asm = [System.Reflection.Assembly]::LoadFrom($dll)
}
catch
{
    Write-Host "Failed to load assembly: $_"
    exit 2
}

$types = $asm.GetTypes() | Where-Object { $_.FullName -like '*Dock*' -or $_.FullName -like '*Docking*' }
if(-not $types)
{
    Write-Host "No dock-related types found in assembly"
    exit 0
}

foreach ($t in $types)
{
    Write-Host "---- $($t.FullName)"
    $members = $t.GetMembers([System.Reflection.BindingFlags] 'Public, Instance, Static') |
               Where-Object { $_.MemberType -in [Reflection.MemberTypes]::Method, [Reflection.MemberTypes]::Property, [Reflection.MemberTypes]::Event } |
               Sort-Object MemberType, Name

    foreach ($m in $members)
    {
        Write-Host "  $($m.MemberType) $($m.Name)"
    }
    Write-Host ""
}

exit 0
