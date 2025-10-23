[CmdletBinding()]
param(
    [string]$Workspace = 'C:\Users\biges\Desktop\Wiley_Widget'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Try to import FsTools if available; otherwise use fallback logic
$fsToolsImported = $false
try {
    if (Get-Module -ListAvailable -Name FsTools) {
        Import-Module FsTools -ErrorAction Stop | Out-Null
        $fsToolsImported = $true
    }
}
catch {
    Write-Verbose ("FsTools import failed: " + $_.Exception.Message)
}

Write-Output 'Searching workspace for *_wpftmp.csproj...'
if ($fsToolsImported) {
    $wpftmp = Find-Files -Pattern '*_wpftmp.csproj' -In $Workspace -Recurse
    $wpftmpPaths = $wpftmp | ForEach-Object { $_.FullName }
}
else {
    $wpftmpPaths = Get-ChildItem -Path $Workspace -Recurse -Filter '*_wpftmp.csproj' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
}
$wpftmpPaths | ForEach-Object { $_ }
Write-Output ("Found: " + (@($wpftmpPaths).Count))

Write-Output 'Searching for csproj under obj folders...'
if ($fsToolsImported) {
    $csprojInObj = Find-Files -Pattern '*.csproj' -In $Workspace -Recurse -IncludeObjBin | Where-Object { $_.FullName -like '*\obj\*' } | Select-Object -ExpandProperty FullName
}
else {
    $csprojInObj = Get-ChildItem -Path $Workspace -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*\obj\*' } | Select-Object -ExpandProperty FullName
}
$csprojInObj | ForEach-Object { $_ }
Write-Output ("Found: " + (@($csprojInObj).Count))
