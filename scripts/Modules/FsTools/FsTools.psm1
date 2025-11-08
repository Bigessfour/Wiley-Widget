Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Get files matching a pattern.

.DESCRIPTION
Searches a directory for files matching a wildcard pattern with options for recursion,
including hidden files, and excluding bin/obj by default.

.PARAMETER Pattern
The wildcard pattern to match (e.g., *.cs).

.PARAMETER In
The directory to search. Defaults to the current location.

.PARAMETER Recurse
Search subdirectories.

.PARAMETER IncludeHidden
Include hidden/system files.

.PARAMETER IncludeObjBin
Include files under bin/ and obj/ directories.

.OUTPUTS
PSCustomObject with FullName, Length, LastWriteTime.

.EXAMPLE
Get-FileMatch -Pattern *.cs -Recurse
#>
function Get-FileMatch {
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

# Back-compat aliases (not exported) for older scripts
Set-Alias -Name Find-File -Value Get-FileMatch -Scope Script
Set-Alias -Name Find-Files -Value Get-FileMatch -Scope Script

<#
.SYNOPSIS
Get text matches across workspace files.

.DESCRIPTION
Searches files matching include globs for a pattern using simple match or regex,
returning path, line number, and line content.

.PARAMETER Pattern
The text or regex to search for.

.PARAMETER In
Root directory to search. Defaults to current location.

.PARAMETER Include
Semicolon-delimited glob patterns. Defaults to common source/project files.

.PARAMETER UseRegex
Use regex matching instead of simple substring match.

.OUTPUTS
PSCustomObject with Path, LineNumber, Line.

.EXAMPLE
Get-WorkspaceTextMatch -Pattern TODO -Include *.cs;*.ps1 -UseRegex:$false
#>
function Get-WorkspaceTextMatch {
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

<#
.SYNOPSIS
Get the project.assets.json content for a project.

.DESCRIPTION
Locates the first project.assets.json under the project's obj folder and returns the
deserialized JSON object. Warns if not found.

.PARAMETER Project
Path to the .csproj file.

.OUTPUTS
Deserialized JSON (PSCustomObject) of project.assets.json.

.EXAMPLE
Get-ProjectAsset -Project .\App\App.csproj
#>
function Get-ProjectAsset {
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Project)
    $proj = (Resolve-Path $Project).Path
    $projDir = Split-Path $proj -Parent
    $assets = Get-ChildItem -Path (Join-Path $projDir 'obj') -Recurse -Filter 'project.assets.json' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    if ($assets) { Get-Content $assets -Raw | ConvertFrom-Json } else { Write-Warning "Assets not found under $projDir\obj" }
}
# Back-compat alias for old plural name (not exported)
Set-Alias -Name Get-ProjectAssets -Value Get-ProjectAsset -Scope Script

<#
.SYNOPSIS
Get temporary WPF project artifact paths.

.DESCRIPTION
Searches common temp locations for files matching *_wpftmp.csproj and returns unique paths.

.PARAMETER In
Optional root to include in the search in addition to TEMP and LOCALAPPDATA.

.OUTPUTS
Unique FullName strings of matching artifacts.

.EXAMPLE
Get-WpfTmpArtifact -In .
#>
function Get-WpfTmpArtifact {
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
# Back-compat alias for old plural name (not exported)
Set-Alias -Name Get-WpfTmpArtifacts -Value Get-WpfTmpArtifact -Scope Script

<#
.SYNOPSIS
Read file content partially or fully.

.DESCRIPTION
Reads the head, tail, or full content of a file.

.PARAMETER Path
File path.

.PARAMETER Head
Number of lines from the start.

.PARAMETER Tail
Number of lines from the end.

.OUTPUTS
String[] lines from the file.

.EXAMPLE
Get-FileText -Path .\README.md -Head 20
#>
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

# Export only approved-verb functions to avoid import warnings
Export-ModuleMember -Function Get-FileMatch, Get-WorkspaceTextMatch, Get-ProjectAsset, Get-WpfTmpArtifact, Get-FileText
