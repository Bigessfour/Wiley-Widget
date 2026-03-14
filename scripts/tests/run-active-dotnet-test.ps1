param(
    [Parameter(Mandatory = $true)]
    [string]$TargetPath,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoRestore,

    [switch]$RunAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
Set-Location $repoRoot

function Normalize-SecretValue {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $trimmed = $Value.Trim()
    if (($trimmed.StartsWith('"') -and $trimmed.EndsWith('"')) -or ($trimmed.StartsWith("'") -and $trimmed.EndsWith("'"))) {
        $trimmed = $trimmed.Substring(1, $trimmed.Length - 2).Trim()
    }

    if ($trimmed.StartsWith('YOUR_SYNCFUSION_LICENSE_KEY', [System.StringComparison]::OrdinalIgnoreCase) -or
        $trimmed.Contains('SYNCFUSION_LICENSE_KEY_HERE', [System.StringComparison]::OrdinalIgnoreCase) -or
        $trimmed.Contains('PLACEHOLDER', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }

    return $trimmed
}

function Get-SyncfusionLicenseKey {
    $candidates = @(
        [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Process'),
        [Environment]::GetEnvironmentVariable('Syncfusion__LicenseKey', 'Process'),
        [Environment]::GetEnvironmentVariable('Syncfusion:LicenseKey', 'Process'),
        [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'User'),
        [Environment]::GetEnvironmentVariable('Syncfusion__LicenseKey', 'User'),
        [Environment]::GetEnvironmentVariable('Syncfusion:LicenseKey', 'User'),
        [Environment]::GetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', 'Machine'),
        [Environment]::GetEnvironmentVariable('Syncfusion__LicenseKey', 'Machine'),
        [Environment]::GetEnvironmentVariable('Syncfusion:LicenseKey', 'Machine')
    )

    foreach ($candidate in $candidates) {
        $normalized = Normalize-SecretValue -Value $candidate
        if (-not [string]::IsNullOrWhiteSpace($normalized)) {
            return $normalized
        }
    }

    $userSecretsProject = Join-Path $repoRoot 'src\WileyWidget.WinForms\WileyWidget.WinForms.csproj'
    if (Test-Path -LiteralPath $userSecretsProject) {
        try {
            $secretLines = & dotnet user-secrets list --project $userSecretsProject 2>$null
            foreach ($line in $secretLines) {
                if ($line -match '^(Syncfusion:LicenseKey|SYNCFUSION_LICENSE_KEY|Syncfusion__LicenseKey)\s*=\s*(.+)$') {
                    $normalized = Normalize-SecretValue -Value $matches[2]
                    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
                        return $normalized
                    }
                }
            }
        } catch {
            Write-Host '[test-runner] Unable to read Syncfusion license from user-secrets; continuing with environment-only lookup.'
        }
    }

    return $null
}

function Stop-StaleTestHosts {
    $testHostProcesses = Get-Process -Name 'testhost', 'testhost.x86', 'testhost.net', 'vstest.console' -ErrorAction SilentlyContinue
    foreach ($process in $testHostProcesses) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            Write-Host "[test-runner] Stopped stale test host process $($process.ProcessName) ($($process.Id))."
        } catch {
            Write-Host "[test-runner] Could not stop stale test host process $($process.ProcessName) ($($process.Id)): $($_.Exception.Message)"
        }
    }
}

function Resolve-TargetItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if (Test-Path -LiteralPath $PathValue) {
        return Get-Item -LiteralPath (Resolve-Path -LiteralPath $PathValue)
    }

    $workspaceRelativePath = Join-Path $repoRoot $PathValue
    if (Test-Path -LiteralPath $workspaceRelativePath) {
        return Get-Item -LiteralPath (Resolve-Path -LiteralPath $workspaceRelativePath)
    }

    throw "TargetPath '$PathValue' was not found. Open the test file you want to run and retry."
}

function Find-NearestProject {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo]$Item
    )

    if ($Item.PSIsContainer) {
        $currentDirectory = $Item.FullName
    } elseif ($Item.Extension -ieq '.csproj') {
        return $Item.FullName
    } else {
        $currentDirectory = Split-Path -Parent $Item.FullName
    }

    while (-not [string]::IsNullOrWhiteSpace($currentDirectory)) {
        $project = Get-ChildItem -LiteralPath $currentDirectory -Filter *.csproj -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($project) {
            return $project.FullName
        }

        $parentDirectory = Split-Path -Parent $currentDirectory
        if ([string]::IsNullOrWhiteSpace($parentDirectory) -or $parentDirectory -eq $currentDirectory) {
            break
        }

        $currentDirectory = $parentDirectory
    }

    throw "No .csproj was found for '$($Item.FullName)'."
}

function Get-TestClassFilter {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $content = Get-Content -LiteralPath $File.FullName -Raw
    $classNames = [System.Collections.Generic.List[string]]::new()

    foreach ($match in [regex]::Matches($content, '(?m)^\s*(?:\[[^\]]+\]\s*)*(?:public|internal|private|protected|sealed|abstract|partial|static\s+)*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)')) {
        $className = $match.Groups['name'].Value
        if (-not [string]::IsNullOrWhiteSpace($className) -and -not $classNames.Contains($className)) {
            $classNames.Add($className)
        }
    }

    if ($classNames.Count -eq 0) {
        $classNames.Add([System.IO.Path]::GetFileNameWithoutExtension($File.Name))
    }

    return ($classNames | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'
}

$targetItem = Resolve-TargetItem -PathValue $TargetPath
$projectPath = Find-NearestProject -Item $targetItem

$syncfusionLicenseKey = Get-SyncfusionLicenseKey
if (-not [string]::IsNullOrWhiteSpace($syncfusionLicenseKey)) {
    [Environment]::SetEnvironmentVariable('SYNCFUSION_LICENSE_KEY', $syncfusionLicenseKey, 'Process')
    [Environment]::SetEnvironmentVariable('Syncfusion__LicenseKey', $syncfusionLicenseKey, 'Process')
    Write-Host '[test-runner] Syncfusion license seeded into the test process environment.'
} else {
    Write-Host '[test-runner] Syncfusion license key not found in environment or user-secrets; Syncfusion UI tests may show a license dialog.'
}

Stop-StaleTestHosts

if ($targetItem.Extension -ieq '.csproj' -or $targetItem.PSIsContainer -or $RunAll.IsPresent) {
    $filter = $null
} elseif ($targetItem.Extension -ieq '.cs') {
    $filter = Get-TestClassFilter -File $targetItem
} else {
    throw "Target '$($targetItem.FullName)' is not a supported test target. Use a .cs test file, a test project, or a test directory."
}

$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$resultsDirectory = Join-Path $repoRoot 'TestResults'
if (-not (Test-Path -LiteralPath $resultsDirectory)) {
    New-Item -ItemType Directory -Path $resultsDirectory | Out-Null
}

$arguments = [System.Collections.Generic.List[string]]::new()
$arguments.Add('test')
$arguments.Add($projectPath)
$arguments.Add('--configuration')
$arguments.Add($Configuration)
if ($NoRestore.IsPresent) {
    $arguments.Add('--no-restore')
}
if (-not [string]::IsNullOrWhiteSpace($filter)) {
    $arguments.Add('--filter')
    $arguments.Add($filter)
}
$arguments.Add('--logger')
$arguments.Add('trx')
$arguments.Add('--results-directory')
$arguments.Add($resultsDirectory)
$arguments.Add('-v')
$arguments.Add('minimal')

Write-Host "Project: $projectName"
Write-Host "Target : $($targetItem.FullName)"
if ($filter) {
    Write-Host "Filter : $filter"
}

& dotnet @arguments
exit $LASTEXITCODE
