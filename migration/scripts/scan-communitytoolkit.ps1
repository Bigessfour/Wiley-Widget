param(
    [string]$Root = "${PSScriptRoot}\..\..",
    [string]$Out = "${PSScriptRoot}\..\reports\communitytoolkit-usage.json"
)

Write-Host "Scanning repository for CommunityToolkit.Mvvm usages..."

if (-not (Test-Path -Path $Root)) {
    throw "Root path not found: $Root"
}

# patterns to search for
$patterns = @{
    "ObservableProperty"        = '\[ObservableProperty\b'
    "RelayCommandAttr"          = '\[RelayCommand\b'
    "ObservableObject"          = '\bObservableObject\b'
    "ObservableRecipient"       = '\bObservableRecipient\b'
    "IRelayCommand"             = '\bIAsync?RelayCommand\b|\bIRelayCommand\b'
    "CommunityToolkitNamespace" = 'CommunityToolkit\.Mvvm'
    "UsingToolkit"              = '^\s*using\s+CommunityToolkit\.Mvvm'
}

$results = @()

# search for candidate files
$files = Get-ChildItem -Path $Root -Recurse -Include *.cs, *.xaml, *.ps1, *.py -File -ErrorAction SilentlyContinue

foreach ($f in $files) {
    $content = Get-Content -Raw -Encoding UTF8 -Path $f.FullName -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($content)) { continue }

    $found = @{ File = $f.FullName; Matches = @() }
    foreach ($k in $patterns.Keys) {
        $rx = $patterns[$k]
        if ($content -match $rx) {
            $found.Matches += @{ Pattern = $k; Regex = $rx }
        }
    }

    if ($found.Matches.Count -gt 0) { $results += $found }
}

if (-not (Test-Path -Path (Split-Path $Out -Parent))) { New-Item -ItemType Directory -Path (Split-Path $Out -Parent) -Force | Out-Null }

$results | ConvertTo-Json -Depth 6 | Set-Content -Path $Out -Encoding UTF8

Write-Host "WROTE: $Out`nFound $($results.Count) files referencing CommunityToolkit patterns."

return $results
