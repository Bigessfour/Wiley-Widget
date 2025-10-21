param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Detailed,
    [switch]$FixIssues
)

function Get-StringSet {
    return [System.Collections.Generic.HashSet[string]]::new()
}

function Convert-FieldNameToProperty {
    param([string]$FieldName)

    if ([string]::IsNullOrWhiteSpace($FieldName)) {
        return $FieldName
    }

    $trimmed = $FieldName.Trim('_')
    if ($trimmed.Contains('_')) {
        $segments = $trimmed.Split('_') | Where-Object { $_ }
        return ($segments | ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) }) -join ''
    }

    return $trimmed.Substring(0, 1).ToUpper() + $trimmed.Substring(1)
}

function Get-DataContextBinding {
    param([string]$XamlContent)

    $match = [regex]::Match($XamlContent, 'DataContext="\{Binding\s+([^,}]+)')
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $null
}

function Get-XamlBindingInfo {
    param([string]$XamlContent)

    $propertySet = Get-StringSet
    $commandSet = Get-StringSet

    $bindingMatches = [regex]::Matches($XamlContent, '\{Binding\s+([^}]+)\}')
    foreach ($bindingMatch in $bindingMatches) {
        $expression = $bindingMatch.Groups[1].Value
        $parts = $expression.Split(',')

        $path = $null
        foreach ($part in $parts) {
            $clean = $part.Trim()
            if ($clean -match '^(Path\s*=\s*)(.+)$') {
                $path = $Matches[2].Trim()
                break
            }
        }

        if (-not $path) {
            $firstPart = $parts[0].Trim()
            if ($firstPart -notmatch '=') {
                $path = $firstPart
            }
        }

        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if ($path -eq '.') { continue }

        $path = $path.Split('.')[0].Trim()
        $path = $path.Split('[')[0].Trim()

        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if ($path -match '^(ElementName|RelativeSource|StaticResource)') { continue }

        if ($path.EndsWith('Command')) {
            $commandSet.Add($path) | Out-Null
        }
        else {
            $propertySet.Add($path) | Out-Null
        }
    }

    return [PSCustomObject]@{
        Properties = $propertySet
        Commands   = $commandSet
    }
}

function Get-ViewInfo {
    param([string]$ProjectRoot)

    $views = @()
    $xamlRoot = Join-Path $ProjectRoot 'src'
    $xamlFiles = Get-ChildItem -Path $xamlRoot -Filter '*.xaml' -Recurse |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

    foreach ($file in $xamlFiles) {
        $name = $file.BaseName
        if ($name -notmatch '(View|Window)$') { continue }

        $content = Get-Content -Path $file.FullName -Raw
        $parsed = Get-XamlBindingInfo -XamlContent $content
        $dataContext = Get-DataContextBinding -XamlContent $content

        $views += [PSCustomObject]@{
            Name            = $name
            XamlPath        = $file.FullName
            DataContext     = $dataContext
            Bindings        = $parsed.Properties
            CommandBindings = $parsed.Commands
        }
    }

    return $views
}

function Get-ViewModelInfo {
    param([string]$ProjectRoot)

    $viewModels = @()
    $codeRoot = Join-Path $ProjectRoot 'src'
    $vmFiles = Get-ChildItem -Path $codeRoot -Filter '*ViewModel.cs' -Recurse |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

    foreach ($file in $vmFiles) {
        $content = Get-Content -Path $file.FullName -Raw

        $properties = Get-StringSet
        $commands = Get-StringSet

        $observableMatches = [regex]::Matches($content, '\[ObservableProperty[\s\S]*?\]\s+private\s+[^\s]+\s+([_\w]+)')
        foreach ($match in $observableMatches) {
            $field = $match.Groups[1].Value
            $propertyName = Convert-FieldNameToProperty -FieldName $field
            if ($propertyName) { $properties.Add($propertyName) | Out-Null }
        }

        $publicPropertyMatches = [regex]::Matches($content, 'public\s+[^\s]+\s+(\w+)\s*\{')
        foreach ($match in $publicPropertyMatches) {
            $properties.Add($match.Groups[1].Value) | Out-Null
        }

        $relayMatches = [regex]::Matches($content, '\[RelayCommand[\s\S]*?\]\s*(?:private|public)?\s*(?:async\s+)?[\w<>]+\s+(\w+)\s*\(')
        foreach ($match in $relayMatches) {
            $methodName = $match.Groups[1].Value
            $commandBase = $methodName -replace 'Async$', ''
            $commands.Add("$commandBase`Command") | Out-Null
        }

        $manualCommandMatches = [regex]::Matches($content, 'public\s+ICommand\s+(\w+)')
        foreach ($match in $manualCommandMatches) {
            $commands.Add($match.Groups[1].Value) | Out-Null
        }

        $viewModels += [PSCustomObject]@{
            Name       = $file.BaseName
            FilePath   = $file.FullName
            Properties = $properties
            Commands   = $commands
        }
    }

    return $viewModels
}

function Get-ViewModelForView {
    param(
        [PSCustomObject]$View,
        [System.Collections.IEnumerable]$ViewModels
    )

    $candidates = @()

    if ($View.DataContext -and $View.DataContext.EndsWith('ViewModel')) {
        $candidates += $View.DataContext
    }

    $candidates += "$($View.Name)ViewModel"

    if ($View.Name -match '^(?<base>.+)View$') {
        $candidates += "$($Matches.base)ViewModel"
    }

    if ($View.Name -match '^(?<base>.+)Window$') {
        $candidates += "$($Matches.base)ViewModel"
    }

    if ($View.Name -match '^(?<base>.+)PanelView$') {
        $candidates += "$($Matches.base)ViewModel"
    }

    $candidates = $candidates | Where-Object { $_ } | Select-Object -Unique

    foreach ($candidate in $candidates) {
        $match = $ViewModels | Where-Object { $_.Name -eq $candidate }
        if ($match) { return $match }
    }

    return $null
}

function Get-ExpectedViewName {
    param([string]$ViewModelName)

    $base = $ViewModelName -replace 'ViewModel$', ''
    return @(
        "$base`View",
        "$base`Window",
        "$base`PanelView"
    )
}

$views = Get-ViewInfo -ProjectRoot $ProjectRoot
$viewModels = Get-ViewModelInfo -ProjectRoot $ProjectRoot

$results = @()

foreach ($view in $views) {
    $vm = Get-ViewModelForView -View $view -ViewModels $viewModels
    if (-not $vm) {
        $results += [PSCustomObject]@{
            Type      = 'MissingViewModel'
            Severity  = 'Error'
            View      = $view.Name
            ViewModel = "$($view.Name)ViewModel"
            Message   = "No matching ViewModel found for view '$($view.Name)'"
            File      = $view.XamlPath
        }
        continue
    }

    foreach ($binding in $view.Bindings) {
        if (-not $vm.Properties.Contains($binding)) {
            $results += [PSCustomObject]@{
                Type      = 'MissingProperty'
                Severity  = 'Error'
                View      = $view.Name
                ViewModel = $vm.Name
                Message   = "Binding '$binding' not found in ViewModel '$($vm.Name)'"
                File      = $view.XamlPath
            }
        }
    }

    foreach ($command in $view.CommandBindings) {
        if (-not $vm.Commands.Contains($command)) {
            $results += [PSCustomObject]@{
                Type      = 'MissingCommand'
                Severity  = 'Error'
                View      = $view.Name
                ViewModel = $vm.Name
                Message   = "Command '$command' not found in ViewModel '$($vm.Name)'"
                File      = $view.XamlPath
            }
        }
    }
}

foreach ($vm in $viewModels) {
    $expectedViews = Get-ExpectedViewName -ViewModelName $vm.Name
    $found = $false
    foreach ($expected in $expectedViews) {
        if ($views.Name -contains $expected) {
            $found = $true
            break
        }
    }

    if (-not $found) {
        $results += [PSCustomObject]@{
            Type      = 'MissingView'
            Severity  = 'Warning'
            View      = ($expectedViews -join ', ')
            ViewModel = $vm.Name
            Message   = "No view found for ViewModel '$($vm.Name)'"
            File      = $vm.FilePath
        }
    }
}

Write-Output "=== VIEW-VIEWMODEL VALIDATION REPORT ==="
Write-Output "Views analyzed   : $($views.Count)"
Write-Output "ViewModels found : $($viewModels.Count)"
Write-Output "Issues detected  : $($results.Count)"

$errors = $results | Where-Object { $_.Severity -eq 'Error' }
$warnings = $results | Where-Object { $_.Severity -eq 'Warning' }

if ($errors) {
    Write-Output "-- Errors ($($errors.Count)) --"
    foreach ($err in $errors) {
        Write-Output "[$($err.Type)] $($err.Message)"
        Write-Output "    View: $($err.View) | ViewModel: $($err.ViewModel)"
        Write-Output "    File: $($err.File)"
    }
}

if ($warnings) {
    Write-Output "-- Warnings ($($warnings.Count)) --"
    foreach ($warn in $warnings) {
        Write-Output "[$($warn.Type)] $($warn.Message)"
        Write-Output "    ViewModel: $($warn.ViewModel)"
        Write-Output "    File: $($warn.File)"
    }
}

if (-not $results) {
    Write-Output "✅ All View-ViewModel relationships validated successfully."
}

if ($Detailed) {
    Write-Output "-- Detailed Mapping --"
    foreach ($view in $views) {
        $vm = Get-ViewModelForView -View $view -ViewModels $viewModels
        $status = if ($vm) { 'OK' } else { 'Missing VM' }
        Write-Output "${status}: $($view.Name)"
    }

    Write-Output "-- Orphaned ViewModels --"
    foreach ($vm in $viewModels) {
        $expectedViews = Get-ExpectedViewName -ViewModelName $vm.Name
        $found = $false
        foreach ($expected in $expectedViews) {
            if ($views.Name -contains $expected) { $found = $true; break }
        }
        if (-not $found) {
            Write-Output "Orphan: $($vm.Name)"
        }
    }
}

if ($FixIssues) {
    Write-Output "Auto-fix mode not implemented."
}
