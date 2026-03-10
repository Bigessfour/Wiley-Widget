<#
.SYNOPSIS
Audits SyncfusionControlFactory coverage against Syncfusion API docs.

.DESCRIPTION
Parses SyncfusionControlFactory.cs and reports, per control type, which API properties,
methods, and events are currently used versus available in the installed Syncfusion API
surface (NuGet XML docs). Produces both Markdown and JSON reports.

.PARAMETER FactoryPath
Path to SyncfusionControlFactory.cs.

.PARAMETER MarkdownOutputPath
Path to write markdown report.

.PARAMETER JsonOutputPath
Path to write JSON report.

.PARAMETER MaxMissingPerCategory
Maximum missing members listed per category (properties/methods/events).

.PARAMETER MinimumPropertyCoverage
If greater than 0, exits non-zero when any audited control falls below this
property coverage percentage.

.EXAMPLE
pwsh -File scripts/audit-syncfusion-factory-completeness.ps1

.EXAMPLE
pwsh -File scripts/audit-syncfusion-factory-completeness.ps1 -MinimumPropertyCoverage 30
#>

[CmdletBinding()]
param(
    [string]$FactoryPath = "src/WileyWidget.WinForms/Factories/SyncfusionControlFactory.cs",
    [string]$MarkdownOutputPath = "tmp/reports/syncfusion-factory-completeness.md",
    [string]$JsonOutputPath = "tmp/reports/syncfusion-factory-completeness.json",
    [string]$DirectoryPackagesPropsPath = "Directory.Packages.props",
    [string]$SyncfusionVersion = "",
    [int]$MaxMissingPerCategory = 12,
    [double]$MinimumPropertyCoverage = 0,
    [bool]$IncludeSamples = $true,
    [string]$SyncfusionSamplesPath = "C:\Program Files (x86)\Syncfusion\Essential Studio\Windows\32.2.3\Samples",
    [int]$MaxSampleFiles = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

trap {
    Write-Host "Audit script failure: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.InvocationInfo.PositionMessage -ForegroundColor Red
    throw
}

function Get-StringSet {
    # Prevent PowerShell from unrolling an empty IEnumerable to $null.
    return , ([System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal))
}

function Add-MatchesToSet {
    param(
        [System.Collections.Generic.HashSet[string]]$Target,
        [Parameter(Mandatory)] [string]$Text,
        [Parameter(Mandatory)] [string]$Pattern,
        [string]$GroupName = "member"
    )

    if ($null -eq $Target) {
        return
    }

    $regexMatches = [regex]::Matches($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    foreach ($m in $regexMatches) {
        $name = $m.Groups[$GroupName].Value
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            [void]$Target.Add($name)
        }
    }
}

function Get-InitializerPropertyNames {
    param(
        [Parameter(Mandatory)] [string]$Snippet,
        [Parameter(Mandatory)] [int]$NewTypeIndex
    )

    $properties = Get-StringSet

    $openBrace = $Snippet.IndexOf('{', $NewTypeIndex)
    if ($openBrace -lt 0) {
        return $properties
    }

    $depth = 0
    $endBrace = -1
    for ($i = $openBrace; $i -lt $Snippet.Length; $i++) {
        $ch = $Snippet[$i]
        if ($ch -eq '{') {
            $depth++
        } elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                $endBrace = $i
                break
            }
        }
    }

    if ($endBrace -le $openBrace) {
        return $properties
    }

    $initializerBody = $Snippet.Substring($openBrace + 1, $endBrace - $openBrace - 1)
    Add-MatchesToSet -Target $properties -Text $initializerBody -Pattern '^[\t ]*(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*=' -GroupName 'member'

    return $properties
}

function Resolve-TypeXmlPath {
    param(
        [Parameter(Mandatory)] [string]$FullTypeName,
        [Parameter(Mandatory)] [string[]]$XmlFiles,
        [Parameter(Mandatory)] [hashtable]$Cache
    )

    if ($Cache.ContainsKey($FullTypeName)) {
        return $Cache[$FullTypeName]
    }

    $escapedType = [regex]::Escape($FullTypeName)
    $pattern = 'name="T:' + $escapedType + '"'
    $hit = Select-String -Path $XmlFiles -Pattern $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $hit) {
        $Cache[$FullTypeName] = $null
        return $null
    }

    $Cache[$FullTypeName] = $hit.Path
    return $hit.Path
}

function Get-TypeApiSurface {
    param(
        [Parameter(Mandatory)] [string]$FullTypeName,
        [Parameter(Mandatory)] [string]$XmlPath,
        [Parameter(Mandatory)] [hashtable]$Cache
    )

    if ($Cache.ContainsKey($FullTypeName)) {
        return $Cache[$FullTypeName]
    }

    $properties = Get-StringSet
    $methods = Get-StringSet
    $events = Get-StringSet

    $escapedType = [regex]::Escape($FullTypeName)
    $lines = Get-Content -Path $XmlPath

    foreach ($line in $lines) {
        if ($line -match 'name="P:' + $escapedType + '\.(?<member>[^"]+)"') {
            [void]$properties.Add($Matches['member'])
            continue
        }

        if ($line -match 'name="M:' + $escapedType + '\.(?<member>[^\("]+)') {
            $method = $Matches['member']
            if (-not $method.StartsWith('#', [System.StringComparison]::Ordinal)) {
                [void]$methods.Add($method)
            }

            continue
        }

        if ($line -match 'name="E:' + $escapedType + '\.(?<member>[^"]+)"') {
            [void]$events.Add($Matches['member'])
            continue
        }
    }

    $surface = [PSCustomObject]@{
        Properties = $properties
        Methods    = $methods
        Events     = $events
    }

    $Cache[$FullTypeName] = $surface
    return $surface
}

function Get-CoveragePercent {
    param(
        [int]$Used,
        [int]$Total,
        [double]$WhenTotalZero = 100.0
    )

    if ($Total -le 0) {
        return $WhenTotalZero
    }

    return [Math]::Round(($Used / $Total) * 100.0, 2)
}

function Resolve-SyncfusionVersionFromProps {
    param(
        [Parameter(Mandatory)] [string]$PropsPath
    )

    if (-not (Test-Path -Path $PropsPath)) {
        return $null
    }

    try {
        [xml]$props = Get-Content -Path $PropsPath
        $syncfusionNodes = $props.Project.ItemGroup.PackageVersion | Where-Object {
            $_.Include -like 'Syncfusion.*' -and $_.Version
        }

        if ($syncfusionNodes -and $syncfusionNodes.Count -gt 0) {
            # Prefer Core WinForms package as canonical for this workspace.
            $preferred = $syncfusionNodes | Where-Object { $_.Include -eq 'Syncfusion.Core.WinForms' } | Select-Object -First 1
            if ($preferred -and $preferred.Version) {
                return [string]$preferred.Version
            }

            return [string]($syncfusionNodes[0].Version)
        }
    } catch {
        Write-Host "Unable to parse $PropsPath for Syncfusion package versions." -ForegroundColor DarkYellow
    }

    return $null
}

function Get-SamplePathCandidates {
    param(
        [Parameter(Mandatory)] [string]$PreferredPath,
        [string]$VersionHint
    )

    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add($PreferredPath)

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        $programFilesX86 = 'C:\Program Files (x86)'
    }

    $windowsRoot = Join-Path $programFilesX86 'Syncfusion\Essential Studio\Windows'
    if (Test-Path -Path $windowsRoot) {
        if (-not [string]::IsNullOrWhiteSpace($VersionHint)) {
            $candidates.Add((Join-Path $windowsRoot (Join-Path $VersionHint 'Samples')))
        }

        # Known fallback commonly used in this repo docs.
        $candidates.Add((Join-Path $windowsRoot '32.1.19\Samples'))

        $latestDirs = Get-ChildItem -Path $windowsRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 3
        foreach ($dir in $latestDirs) {
            $candidates.Add((Join-Path $dir.FullName 'Samples'))
        }
    }

    return @($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Get-SampleCodeFiles {
    param(
        [Parameter(Mandatory)] [string[]]$CandidatePaths,
        [int]$MaxFiles = 50
    )

    $resolvedPath = $null
    $files = @()

    foreach ($path in $CandidatePaths) {
        if (-not (Test-Path -Path $path)) {
            continue
        }

        $resolvedPath = $path
        $files = @(Get-ChildItem -Path (Join-Path $path '*') -Recurse -Include *.cs -File -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName)

        if ($files.Count -gt 0) {
            break
        }
    }

    if ($MaxFiles -gt 0 -and $files.Count -gt $MaxFiles) {
        $files = @($files | Select-Object -First $MaxFiles)
    }

    return [PSCustomObject]@{
        ResolvedPath = $resolvedPath
        Files        = $files
    }
}

function Get-ControlUsageFromText {
    param(
        [Parameter(Mandatory)] [string]$Text,
        [Parameter(Mandatory)] [string]$ShortType,
        [int]$SnippetWindow = 5000
    )

    $usedProperties = Get-StringSet
    $usedMethods = Get-StringSet
    $usedEvents = Get-StringSet

    $pattern = '(?ms)(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+' + [regex]::Escape($ShortType) + '(?:<[^>]+>)?\s*(?<tail>[\(\{])'
    $instantiations = [regex]::Matches($Text, $pattern)

    foreach ($instantiation in $instantiations) {
        $varName = $instantiation.Groups['var'].Value
        if ([string]::IsNullOrWhiteSpace($varName)) {
            continue
        }

        $snippetStart = $instantiation.Index
        $snippetLength = [Math]::Min($SnippetWindow, $Text.Length - $snippetStart)
        if ($snippetLength -le 0) {
            continue
        }

        $snippet = $Text.Substring($snippetStart, $snippetLength)
        $newTypeLocalIndex = $snippet.IndexOf('new ' + $ShortType, [System.StringComparison]::Ordinal)

        if ($newTypeLocalIndex -ge 0) {
            $initializerProperties = Get-InitializerPropertyNames -Snippet $snippet -NewTypeIndex $newTypeLocalIndex
            foreach ($p in $initializerProperties) {
                [void]$usedProperties.Add($p)
            }
        }

        $escapedVar = [regex]::Escape($varName)
        Add-MatchesToSet -Target $usedProperties -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*="
        Add-MatchesToSet -Target $usedProperties -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\."
        Add-MatchesToSet -Target $usedMethods -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\("
        Add-MatchesToSet -Target $usedEvents -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\+="
    }

    return [PSCustomObject]@{
        InstantiationCount = $instantiations.Count
        Properties         = $usedProperties
        Methods            = $usedMethods
        Events             = $usedEvents
    }
}

$controlMap = @(
    [PSCustomObject]@{ ShortType = 'SfDataGrid'; FullType = 'Syncfusion.WinForms.DataGrid.SfDataGrid' },
    [PSCustomObject]@{ ShortType = 'SfButton'; FullType = 'Syncfusion.WinForms.Controls.SfButton' },
    [PSCustomObject]@{ ShortType = 'ChartControl'; FullType = 'Syncfusion.Windows.Forms.Chart.ChartControl' },
    [PSCustomObject]@{ ShortType = 'TabControlAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.TabControlAdv' },
    [PSCustomObject]@{ ShortType = 'RibbonControlAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.RibbonControlAdv' },
    [PSCustomObject]@{ ShortType = 'SfListView'; FullType = 'Syncfusion.WinForms.ListView.SfListView' },
    [PSCustomObject]@{ ShortType = 'AutoComplete'; FullType = 'Syncfusion.Windows.Forms.Tools.AutoComplete' },
    [PSCustomObject]@{ ShortType = 'PdfViewerControl'; FullType = 'Syncfusion.Windows.Forms.PdfViewer.PdfViewerControl' },
    [PSCustomObject]@{ ShortType = 'TextBoxExt'; FullType = 'Syncfusion.Windows.Forms.Tools.TextBoxExt' },
    [PSCustomObject]@{ ShortType = 'SfComboBox'; FullType = 'Syncfusion.WinForms.ListView.SfComboBox' },
    [PSCustomObject]@{ ShortType = 'SfDateTimeEdit'; FullType = 'Syncfusion.WinForms.Input.SfDateTimeEdit' },
    [PSCustomObject]@{ ShortType = 'DateTimePickerAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.DateTimePickerAdv' },
    [PSCustomObject]@{ ShortType = 'SfNumericTextBox'; FullType = 'Syncfusion.WinForms.Input.SfNumericTextBox' },
    [PSCustomObject]@{ ShortType = 'CheckBoxAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.CheckBoxAdv' },
    [PSCustomObject]@{ ShortType = 'NumericUpDownExt'; FullType = 'Syncfusion.Windows.Forms.Tools.NumericUpDownExt' },
    [PSCustomObject]@{ ShortType = 'SplitContainerAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.SplitContainerAdv' },
    [PSCustomObject]@{ ShortType = 'ProgressBarAdv'; FullType = 'Syncfusion.Windows.Forms.Tools.ProgressBarAdv' },
    [PSCustomObject]@{ ShortType = 'RadialGauge'; FullType = 'Syncfusion.Windows.Forms.Gauge.RadialGauge' }
)

if (-not (Test-Path -Path $FactoryPath)) {
    throw "Factory file not found: $FactoryPath"
}

$resolvedSyncfusionVersion = if (-not [string]::IsNullOrWhiteSpace($SyncfusionVersion)) {
    $SyncfusionVersion.Trim()
} else {
    Resolve-SyncfusionVersionFromProps -PropsPath $DirectoryPackagesPropsPath
}

$nugetRoot = Join-Path $HOME ".nuget/packages"
if (-not (Test-Path -Path $nugetRoot)) {
    throw "NuGet package cache not found: $nugetRoot"
}

$allSyncfusionXmlFiles = @(Get-ChildItem -Path $nugetRoot -Recurse -Filter '*.xml' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\syncfusion\.' } |
    Select-Object -ExpandProperty FullName)

$syncfusionXmlFiles = $allSyncfusionXmlFiles
if (-not [string]::IsNullOrWhiteSpace($resolvedSyncfusionVersion)) {
    $versionPattern = '\\syncfusion\.[^\\]+\\' + [regex]::Escape($resolvedSyncfusionVersion) + '\\'
    $versionFiltered = @($allSyncfusionXmlFiles | Where-Object { $_ -match $versionPattern })

    if ($versionFiltered.Count -gt 0) {
        $syncfusionXmlFiles = $versionFiltered
    } else {
        Write-Host "No Syncfusion XML docs found for version $resolvedSyncfusionVersion in local NuGet cache. Falling back to all installed Syncfusion XML docs." -ForegroundColor DarkYellow
    }
}

if (-not $syncfusionXmlFiles -or $syncfusionXmlFiles.Count -eq 0) {
    throw "No Syncfusion XML API documentation files found in $nugetRoot"
}

$factoryText = Get-Content -Path $FactoryPath -Raw

$sampleScanEnabled = $false
$sampleCodeFiles = @()
$resolvedSamplePath = $null

if ($IncludeSamples) {
    $sampleCandidates = Get-SamplePathCandidates -PreferredPath $SyncfusionSamplesPath -VersionHint $resolvedSyncfusionVersion
    $sampleLookup = Get-SampleCodeFiles -CandidatePaths $sampleCandidates -MaxFiles $MaxSampleFiles
    $sampleCodeFiles = @($sampleLookup.Files)

    if (-not [string]::IsNullOrWhiteSpace($sampleLookup.ResolvedPath)) {
        $resolvedSamplePath = $sampleLookup.ResolvedPath
    }

    $sampleScanEnabled = $sampleCodeFiles.Count -gt 0
    if (-not $sampleScanEnabled) {
        Write-Host "No sample .cs files found in candidate Syncfusion sample paths. Sample-backed analysis disabled." -ForegroundColor Yellow
    } else {
        Write-Host "Sample-backed analysis using: $resolvedSamplePath ($($sampleCodeFiles.Count) files)" -ForegroundColor DarkGreen
    }
}

$typeXmlCache = @{}
$typeApiCache = @{}
$results = @()

foreach ($entry in $controlMap) {
    $shortType = $entry.ShortType
    $fullType = $entry.FullType

    $usedProperties = Get-StringSet
    $usedMethods = Get-StringSet
    $usedEvents = Get-StringSet

    $pattern = '(?ms)(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+' + [regex]::Escape($shortType) + '(?:<[^>]+>)?\s*(?<tail>[\(\{])'
    $instantiations = [regex]::Matches($factoryText, $pattern)
    foreach ($instantiation in $instantiations) {
        $varName = $instantiation.Groups['var'].Value
        if ([string]::IsNullOrWhiteSpace($varName)) {
            continue
        }

        $snippetStart = $instantiation.Index
        $nextMethodMatch = [regex]::Match($factoryText.Substring($snippetStart), '(?m)^\s*public\s+')
        $snippetLength = if ($nextMethodMatch.Success -and $nextMethodMatch.Index -gt 0) {
            $nextMethodMatch.Index
        } else {
            $factoryText.Length - $snippetStart
        }

        $snippet = $factoryText.Substring($snippetStart, $snippetLength)

        $newTypeLocalIndex = $snippet.IndexOf('new ' + $shortType, [System.StringComparison]::Ordinal)
        if ($newTypeLocalIndex -ge 0) {
            $initializerProperties = Get-InitializerPropertyNames -Snippet $snippet -NewTypeIndex $newTypeLocalIndex
            foreach ($p in $initializerProperties) {
                [void]$usedProperties.Add($p)
            }
        }

        $escapedVar = [regex]::Escape($varName)

        Add-MatchesToSet -Target $usedProperties -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*="
        Add-MatchesToSet -Target $usedProperties -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\."
        Add-MatchesToSet -Target $usedMethods -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\("
        Add-MatchesToSet -Target $usedEvents -Text $snippet -Pattern "\b$escapedVar\.(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\+="
    }

    $sampleUsedProperties = Get-StringSet
    $sampleUsedMethods = Get-StringSet
    $sampleUsedEvents = Get-StringSet
    $sampleInstantiationCount = 0

    if ($sampleScanEnabled) {
        $samplePattern = 'new\s+' + [regex]::Escape($shortType) + '(?:<[^>]+>)?\s*[\(\{]'
        $sampleHits = Select-String -Path $sampleCodeFiles -Pattern $samplePattern -ErrorAction SilentlyContinue
        $sampleFilesForControl = @($sampleHits | Select-Object -ExpandProperty Path -Unique)

        foreach ($sampleFile in $sampleFilesForControl) {
            try {
                $sampleText = Get-Content -Path $sampleFile -Raw
                $sampleUsage = Get-ControlUsageFromText -Text $sampleText -ShortType $shortType
                $sampleInstantiationCount += $sampleUsage.InstantiationCount

                foreach ($p in $sampleUsage.Properties) {
                    [void]$sampleUsedProperties.Add($p)
                }

                foreach ($m in $sampleUsage.Methods) {
                    [void]$sampleUsedMethods.Add($m)
                }

                foreach ($e in $sampleUsage.Events) {
                    [void]$sampleUsedEvents.Add($e)
                }
            } catch {
                Write-Host "Unable to parse sample file: $sampleFile" -ForegroundColor DarkYellow
            }
        }
    }

    $sampleUsedPropertiesArr = @($sampleUsedProperties | Sort-Object)
    $sampleUsedMethodsArr = @($sampleUsedMethods | Sort-Object)
    $sampleUsedEventsArr = @($sampleUsedEvents | Sort-Object)

    $sampleBackedMissingProperties = @($sampleUsedPropertiesArr | Where-Object { $usedProperties.Contains($_) -eq $false })
    $sampleBackedMissingMethods = @($sampleUsedMethodsArr | Where-Object { $usedMethods.Contains($_) -eq $false })
    $sampleBackedMissingEvents = @($sampleUsedEventsArr | Where-Object { $usedEvents.Contains($_) -eq $false })

    $samplePropertyCoverage = Get-CoveragePercent -Used ($sampleUsedPropertiesArr.Count - $sampleBackedMissingProperties.Count) -Total $sampleUsedPropertiesArr.Count -WhenTotalZero 0.0
    $sampleMethodCoverage = Get-CoveragePercent -Used ($sampleUsedMethodsArr.Count - $sampleBackedMissingMethods.Count) -Total $sampleUsedMethodsArr.Count -WhenTotalZero 0.0
    $sampleEventCoverage = Get-CoveragePercent -Used ($sampleUsedEventsArr.Count - $sampleBackedMissingEvents.Count) -Total $sampleUsedEventsArr.Count -WhenTotalZero 0.0

    $xmlPath = Resolve-TypeXmlPath -FullTypeName $fullType -XmlFiles $syncfusionXmlFiles -Cache $typeXmlCache
    if (-not $xmlPath) {
        $results += [PSCustomObject]@{
            Control                       = $shortType
            FullType                      = $fullType
            XmlPath                       = $null
            Instantiations                = $instantiations.Count
            SampleInstantiations          = $sampleInstantiationCount
            ApiProperties                 = 0
            ApiMethods                    = 0
            ApiEvents                     = 0
            UsedProperties                = @($usedProperties | Sort-Object)
            UsedMethods                   = @($usedMethods | Sort-Object)
            UsedEvents                    = @($usedEvents | Sort-Object)
            SampleUsedProperties          = $sampleUsedPropertiesArr
            SampleUsedMethods             = $sampleUsedMethodsArr
            SampleUsedEvents              = $sampleUsedEventsArr
            MissingProperties             = @()
            MissingMethods                = @()
            MissingEvents                 = @()
            SampleBackedMissingProperties = $sampleBackedMissingProperties
            SampleBackedMissingMethods    = $sampleBackedMissingMethods
            SampleBackedMissingEvents     = $sampleBackedMissingEvents
            PropertyCoveragePercent       = 0.0
            MethodCoveragePercent         = 0.0
            EventCoveragePercent          = 0.0
            SamplePropertyCoveragePercent = $samplePropertyCoverage
            SampleMethodCoveragePercent   = $sampleMethodCoverage
            SampleEventCoveragePercent    = $sampleEventCoverage
            Notes                         = "API XML type definition not found in local NuGet docs"
        }

        continue
    }

    $api = Get-TypeApiSurface -FullTypeName $fullType -XmlPath $xmlPath -Cache $typeApiCache

    $apiProperties = @($api.Properties | Sort-Object)
    $apiMethods = @($api.Methods | Sort-Object)
    $apiEvents = @($api.Events | Sort-Object)

    $usedPropertiesArr = @($usedProperties | Sort-Object)
    $usedMethodsArr = @($usedMethods | Sort-Object)
    $usedEventsArr = @($usedEvents | Sort-Object)

    $missingProperties = @($apiProperties | Where-Object { $usedProperties.Contains($_) -eq $false })
    $missingMethods = @($apiMethods | Where-Object { $usedMethods.Contains($_) -eq $false })
    $missingEvents = @($apiEvents | Where-Object { $usedEvents.Contains($_) -eq $false })

    $results += [PSCustomObject]@{
        Control                       = $shortType
        FullType                      = $fullType
        XmlPath                       = $xmlPath
        Instantiations                = $instantiations.Count
        SampleInstantiations          = $sampleInstantiationCount
        ApiProperties                 = $apiProperties.Count
        ApiMethods                    = $apiMethods.Count
        ApiEvents                     = $apiEvents.Count
        UsedProperties                = $usedPropertiesArr
        UsedMethods                   = $usedMethodsArr
        UsedEvents                    = $usedEventsArr
        SampleUsedProperties          = $sampleUsedPropertiesArr
        SampleUsedMethods             = $sampleUsedMethodsArr
        SampleUsedEvents              = $sampleUsedEventsArr
        MissingProperties             = $missingProperties
        MissingMethods                = $missingMethods
        MissingEvents                 = $missingEvents
        SampleBackedMissingProperties = $sampleBackedMissingProperties
        SampleBackedMissingMethods    = $sampleBackedMissingMethods
        SampleBackedMissingEvents     = $sampleBackedMissingEvents
        PropertyCoveragePercent       = Get-CoveragePercent -Used $usedPropertiesArr.Count -Total $apiProperties.Count
        MethodCoveragePercent         = Get-CoveragePercent -Used $usedMethodsArr.Count -Total $apiMethods.Count
        EventCoveragePercent          = Get-CoveragePercent -Used $usedEventsArr.Count -Total $apiEvents.Count
        SamplePropertyCoveragePercent = $samplePropertyCoverage
        SampleMethodCoveragePercent   = $sampleMethodCoverage
        SampleEventCoveragePercent    = $sampleEventCoverage
        Notes                         = $null
    }
}

$results = $results | Sort-Object Control

$overall = [PSCustomObject]@{
    ControlsAudited                      = $results.Count
    ControlsWithInstantiations           = ($results | Where-Object { $_.Instantiations -gt 0 }).Count
    SyncfusionVersion                    = $resolvedSyncfusionVersion
    SampleScanEnabled                    = $sampleScanEnabled
    SamplePath                           = if ([string]::IsNullOrWhiteSpace($resolvedSamplePath)) { 'not found' } else { $resolvedSamplePath }
    SampleFilesScanned                   = $sampleCodeFiles.Count
    GeneratedAtUtc                       = [DateTime]::UtcNow.ToString('u')
    AveragePropertyCoveragePercent       = [Math]::Round((($results | Measure-Object -Property PropertyCoveragePercent -Average).Average), 2)
    AverageMethodCoveragePercent         = [Math]::Round((($results | Measure-Object -Property MethodCoveragePercent -Average).Average), 2)
    AverageEventCoveragePercent          = [Math]::Round((($results | Measure-Object -Property EventCoveragePercent -Average).Average), 2)
    AverageSamplePropertyCoveragePercent = [Math]::Round((($results | Measure-Object -Property SamplePropertyCoveragePercent -Average).Average), 2)
    AverageSampleMethodCoveragePercent   = [Math]::Round((($results | Measure-Object -Property SampleMethodCoveragePercent -Average).Average), 2)
    AverageSampleEventCoveragePercent    = [Math]::Round((($results | Measure-Object -Property SampleEventCoveragePercent -Average).Average), 2)
}

$markdownLines = [System.Collections.Generic.List[string]]::new()
$markdownLines.Add('# Syncfusion Factory Completeness Audit')
$markdownLines.Add('')
$markdownLines.Add("Generated (UTC): $($overall.GeneratedAtUtc)")
$markdownLines.Add('Factory: `' + $FactoryPath + '`')
$markdownLines.Add('- Syncfusion version: `' + $(if ([string]::IsNullOrWhiteSpace($overall.SyncfusionVersion)) { 'unknown' } else { $overall.SyncfusionVersion }) + '`')
$markdownLines.Add('')
$markdownLines.Add('## Summary')
$markdownLines.Add('')
$markdownLines.Add("- Controls audited: $($overall.ControlsAudited)")
$markdownLines.Add("- Controls instantiated in factory: $($overall.ControlsWithInstantiations)")
$markdownLines.Add("- Sample scan enabled: $($overall.SampleScanEnabled)")
$markdownLines.Add("- Sample files scanned: $($overall.SampleFilesScanned)")
$markdownLines.Add('- Sample path: `' + $overall.SamplePath + '`')
$markdownLines.Add("- Avg property coverage: $($overall.AveragePropertyCoveragePercent)%")
$markdownLines.Add("- Avg method coverage: $($overall.AverageMethodCoveragePercent)%")
$markdownLines.Add("- Avg event coverage: $($overall.AverageEventCoveragePercent)%")
$markdownLines.Add("- Avg sample-backed property coverage: $($overall.AverageSamplePropertyCoveragePercent)%")
$markdownLines.Add("- Avg sample-backed method coverage: $($overall.AverageSampleMethodCoveragePercent)%")
$markdownLines.Add("- Avg sample-backed event coverage: $($overall.AverageSampleEventCoveragePercent)%")
$markdownLines.Add('')
$markdownLines.Add('## Coverage Table')
$markdownLines.Add('')
$markdownLines.Add('| Control | Instantiations | Sample Instantiations | API Property Coverage | Sample Property Coverage | API P/M/E | Used P/M/E | Sample Used P/M/E |')
$markdownLines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')

foreach ($row in $results) {
    $markdownLines.Add("| $($row.Control) | $($row.Instantiations) | $($row.SampleInstantiations) | $($row.PropertyCoveragePercent)% | $($row.SamplePropertyCoveragePercent)% | $($row.ApiProperties) / $($row.ApiMethods) / $($row.ApiEvents) | $($row.UsedProperties.Count) / $($row.UsedMethods.Count) / $($row.UsedEvents.Count) | $($row.SampleUsedProperties.Count) / $($row.SampleUsedMethods.Count) / $($row.SampleUsedEvents.Count) |")
}

$markdownLines.Add('')
$markdownLines.Add('## Missing Members (Top)')
$markdownLines.Add('')

foreach ($row in $results) {
    $markdownLines.Add("### $($row.Control)")

    if ($row.Notes) {
        $markdownLines.Add('')
        $markdownLines.Add("- Note: $($row.Notes)")
        $markdownLines.Add('')
        continue
    }

    $missingPropertiesTop = @($row.MissingProperties | Select-Object -First $MaxMissingPerCategory)
    $missingMethodsTop = @($row.MissingMethods | Select-Object -First $MaxMissingPerCategory)
    $missingEventsTop = @($row.MissingEvents | Select-Object -First $MaxMissingPerCategory)
    $sampleMissingPropertiesTop = @($row.SampleBackedMissingProperties | Select-Object -First $MaxMissingPerCategory)
    $sampleMissingMethodsTop = @($row.SampleBackedMissingMethods | Select-Object -First $MaxMissingPerCategory)
    $sampleMissingEventsTop = @($row.SampleBackedMissingEvents | Select-Object -First $MaxMissingPerCategory)

    $markdownLines.Add('')
    $markdownLines.Add("- Missing properties ($($row.MissingProperties.Count)): $([string]::Join(', ', $missingPropertiesTop))")
    $markdownLines.Add("- Missing methods ($($row.MissingMethods.Count)): $([string]::Join(', ', $missingMethodsTop))")
    $markdownLines.Add("- Missing events ($($row.MissingEvents.Count)): $([string]::Join(', ', $missingEventsTop))")
    $markdownLines.Add("- Sample-backed missing properties ($($row.SampleBackedMissingProperties.Count)): $([string]::Join(', ', $sampleMissingPropertiesTop))")
    $markdownLines.Add("- Sample-backed missing methods ($($row.SampleBackedMissingMethods.Count)): $([string]::Join(', ', $sampleMissingMethodsTop))")
    $markdownLines.Add("- Sample-backed missing events ($($row.SampleBackedMissingEvents.Count)): $([string]::Join(', ', $sampleMissingEventsTop))")
    $markdownLines.Add('')
}

$markdownDir = Split-Path -Path $MarkdownOutputPath -Parent
$jsonDir = Split-Path -Path $JsonOutputPath -Parent
if ($markdownDir) {
    [void](New-Item -ItemType Directory -Path $markdownDir -Force)
}
if ($jsonDir) {
    [void](New-Item -ItemType Directory -Path $jsonDir -Force)
}

$markdownLines -join "`n" | Set-Content -Path $MarkdownOutputPath -Encoding utf8

$payload = [PSCustomObject]@{
    Overall  = $overall
    Controls = $results
}
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $JsonOutputPath -Encoding utf8

Write-Host "Syncfusion factory completeness report generated:" -ForegroundColor Green
Write-Host "- Markdown: $MarkdownOutputPath"
Write-Host "- JSON:     $JsonOutputPath"

$belowThreshold = @()
if ($MinimumPropertyCoverage -gt 0) {
    $belowThreshold = $results | Where-Object {
        $_.Instantiations -gt 0 -and $_.PropertyCoveragePercent -lt $MinimumPropertyCoverage
    }
}

if ($belowThreshold.Count -gt 0) {
    Write-Host "Controls below MinimumPropertyCoverage ($MinimumPropertyCoverage%):" -ForegroundColor Yellow
    foreach ($row in $belowThreshold) {
        Write-Host "- $($row.Control): $($row.PropertyCoveragePercent)%" -ForegroundColor Yellow
    }

    exit 1
}

exit 0
