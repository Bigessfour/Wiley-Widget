Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-Files {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Pattern,
        [string]$In = (Get-Location).Path,
        [switch]$Recurse,
        [switch]$IncludeHidden,
        [switch]$IncludeObjBin
    )
    $dir = (Resolve-Path $In).Path
    $searchParams = @{ Path = $dir; Filter = $Pattern; ErrorAction = 'SilentlyContinue' }
    if ($Recurse) { $searchParams['Recurse'] = $true }
    Get-ChildItem @searchParams |
        Where-Object {
            ($IncludeHidden -or (-not ($_.Attributes.HasFlag([IO.FileAttributes]::Hidden) -or $_.Attributes.HasFlag([IO.FileAttributes]::System)))) -and
            ($IncludeObjBin -or (($_.FullName -notlike '*\bin\*') -and ($_.FullName -notlike '*\obj\*')))
        } |
        Select-Object FullName, Length, LastWriteTime
}

function RipGrep-Workspace {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Pattern,
        [string]$In = (Get-Location).Path,
        [string]$Include = '*.cs;*.xaml;*.csproj;*.props;*.targets;*.ps1',
        [switch]$UseRegex
    )
    $dir = (Resolve-Path $In).Path
    $includeGlobs = $Include.Split(';')
    $files = Get-ChildItem -Path $dir -Recurse -File -Include $includeGlobs -ErrorAction SilentlyContinue
    if ($UseRegex) {
        $files | Select-String -Pattern $Pattern -AllMatches | ForEach-Object {
            [pscustomobject]@{ Path = $_.Path; LineNumber = $_.LineNumber; Line = $_.Line }
        }
    }
    else {
        $files | Select-String -SimpleMatch -Pattern $Pattern -AllMatches | ForEach-Object {
            [pscustomobject]@{ Path = $_.Path; LineNumber = $_.LineNumber; Line = $_.Line }
        }
    }
}

function Get-ProjectAssets {
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Project)
    $proj = (Resolve-Path $Project).Path
    $projDir = Split-Path $proj -Parent
    $assets = Get-ChildItem -Path (Join-Path $projDir 'obj') -Recurse -Filter 'project.assets.json' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($assets) { Get-Content $assets -Raw | ConvertFrom-Json } else { Write-Warning "Assets not found under $projDir\obj" }
}

function Get-WpfTmpArtifacts {
    [CmdletBinding()] param([string]$In = (Get-Location).Path)
    $results = @()
    foreach ($root in @($In, $env:TEMP, $env:LOCALAPPDATA)) {
        if ($root) {
            $resolved = Resolve-Path $root -ErrorAction SilentlyContinue
            if ($resolved) {
                $results += Get-ChildItem -Path $resolved -Recurse -Filter '*_wpftmp.csproj' -ErrorAction SilentlyContinue
            }
        }
    }
    $results | Select-Object -Unique FullName
}

function Get-FileText {
    [CmdletBinding()] param(
        [Parameter(Mandatory)][string]$Path,
        [int]$Head,
        [int]$Tail
    )
    if ($Head) { Get-Content -Path $Path -TotalCount $Head }
    elseif ($Tail) { Get-Content -Path $Path -Tail $Tail }
    else { Get-Content -Path $Path }
}

Export-ModuleMember -Function Find-Files, RipGrep-Workspace, Get-ProjectAssets, Get-WpfTmpArtifacts, Get-FileText
