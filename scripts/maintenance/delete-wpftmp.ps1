[CmdletBinding()]
param(
    [string]$Workspace = 'C:\\Users\\biges\\Desktop\\Wiley-Widget'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Try to import FsTools; fall back to Get-ChildItem
$targets = @()
try {
    if (Get-Module -ListAvailable -Name FsTools) {
        Import-Module FsTools -ErrorAction Stop | Out-Null
        $targets = Get-FileMatch -Pattern '*_wpftmp.csproj' -In $Workspace -Recurse | Select-Object -ExpandProperty FullName
    }
    else {
        $targets = Get-ChildItem -Path $Workspace -Recurse -Filter '*_wpftmp.csproj' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
    }
}
catch {
    $targets = Get-ChildItem -Path $Workspace -Recurse -Filter '*_wpftmp.csproj' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
}

$targets = @($targets)
if ($targets.Count -eq 0) {
    Write-Output 'No *_wpftmp.csproj files found.'
    exit 0
}
Write-Output 'Deleting the following files:'
$targets | ForEach-Object { Write-Output " - $_" }
$failed = @()
foreach ($f in $targets) {
    try {
        Remove-Item -LiteralPath $f -Force -ErrorAction Stop
        Write-Output "Deleted: $f"
    }
    catch {
        $failed += $f
        Write-Warning "Failed to delete: $f - $($_.Exception.Message)"
    }
}
if ($failed.Count -gt 0) {
    Write-Output "Failures: $($failed.Count)"
    exit 1
}
else {
    Write-Output 'All wpftmp files deleted.'
    exit 0
}
