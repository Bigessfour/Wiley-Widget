[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Detailed,
    [switch]$FixIssues
)

function Get-StringSet {
    try {
        return [System.Collections.Generic.HashSet[string]]::new()
    }
    catch {
        # Fallback for PowerShell editions where generic HashSet construction may fail
        return [System.Collections.ArrayList]@()
    }
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
    if (-not $propertySet) { $propertySet = [System.Collections.ArrayList]@() }
    $commandSet = Get-StringSet
    if (-not $commandSet) { $commandSet = [System.Collections.ArrayList]@() }

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
        if (-not $properties) { $properties = [System.Collections.ArrayList]@() }
        $commands = Get-StringSet
        if (-not $commands) { $commands = [System.Collections.ArrayList]@() }

        # Detect Prism source-generator DelegateCommand usage
        $delegateMatches = [regex]::Matches($content, 'DelegateCommand[\s\S]*?\]\s+private\s+[^\s]+\s+([_\w]+)')
        foreach ($match in $delegateMatches) {
            $field = $match.Groups[1].Value
            $propertyName = Convert-FieldNameToProperty -FieldName $field
            if ($propertyName) { $properties.Add($propertyName) | Out-Null }
        }

        # Detect Prism MVVM patterns
        $usesPrismMvvm = [regex]::IsMatch($content, 'Prism\.Mvvm|BindableBase|RaisePropertyChanged')
        $usesPrismCommands = [regex]::IsMatch($content, 'Prism\.Commands|DelegateCommand')

        # Detect DelegateCommand initialization patterns
        $delegateCommandMatches = [regex]::Matches($content, 'DelegateCommand[\s\S]*?\]\s*(?:private|public)?\s*(?:async\s+)?[\w<>]+\s+(\w+)\s*\(')
        foreach ($match in $delegateCommandMatches) {
            $methodName = $match.Groups[1].Value
            $commandBase = $methodName -replace 'Async$', ''
            $commands.Add("${commandBase}Command") | Out-Null
        }

        # Detect usage of IRelayCommand/IAsyncRelayCommand or ObservableRecipient types
        $interfaceRelayMatches = [regex]::Matches($content, '\bI(A?RelayCommand)\b')
        foreach ($m in $interfaceRelayMatches) { $commands.Add($m.Groups[1].Value) | Out-Null }

        # Detect legacy/simple IoC usage that is not Prism.Unity or Prism DI (e.g., SimpleIoc, ServiceLocator, Ioc.Default, ContainerLocator, GalaSoft)
        $legacyIoCPatterns = @('SimpleIoc', 'ServiceLocator', 'ContainerLocator', 'Ioc\.Default', 'GalaSoft', 'DependencyService', 'StashBox', 'Autofac')
        $foundLegacyIoC = @()
        foreach ($pat in $legacyIoCPatterns) {
            if ($content -match $pat) { $foundLegacyIoC += $pat }
        }

        $publicPropertyMatches = [regex]::Matches($content, 'public\s+[^\s]+\s+(\w+)\s*\{')
        foreach ($match in $publicPropertyMatches) {
            $properties.Add($match.Groups[1].Value) | Out-Null
        }

        $relayMatches = [regex]::Matches($content, '\[RelayCommand[\s\S]*?\]\s*(?:private|public)?\s*(?:async\s+)?[\w<>]+\s+(\w+)\s*\(')
        foreach ($match in $relayMatches) {
            $methodName = $match.Groups[1].Value
            $commandBase = $methodName -replace 'Async$', ''
            $commands.Add("${commandBase}Command") | Out-Null
        }

        $manualCommandMatches = [regex]::Matches($content, 'public\s+(?:System\.Windows\.Input\.)?ICommand\s+(\w+)')
        foreach ($match in $manualCommandMatches) {
            $commands.Add($match.Groups[1].Value) | Out-Null
        }

        $viewModels += [PSCustomObject]@{
            Name              = $file.BaseName
            FilePath          = $file.FullName
            Properties        = $properties
            Commands          = $commands
            UsesPrismMvvm     = $usesPrismMvvm
            UsesPrismCommands = $usesPrismCommands
            LegacyIoC         = ($foundLegacyIoC -join ', ')
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
        "${base}View",
        "${base}Window",
        "${base}PanelView"
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


    function Get-BaseMember {
        param([string]$BaseFile = (Join-Path $ProjectRoot 'src\ViewModels\Base\AsyncViewModelBase.cs'))

        $members = @()
        if (Test-Path $BaseFile) {
            $bContent = Get-Content -Path $BaseFile -Raw
            $bMatches = [regex]::Matches($bContent, 'public\s+[^{;(]+\s+(\w+)\s*(\{|\()')
            foreach ($m in $bMatches) { $members += $m.Groups[1].Value }
        }
        return $members | Select-Object -Unique
    }

    $baseMembers = Get-BaseMember

    foreach ($vm in $viewModels) {
        $content = Get-Content -Path $vm.FilePath -Raw
        foreach ($member in $baseMembers) {
            # If VM declares a public member with same name but without 'new', warn
            $pattern = "public\s+(?!new\b)[^\n\r]+\b$member\b"
            if ([regex]::IsMatch($content, $pattern)) {
                $results += [PSCustomObject]@{
                    Type      = 'HidingMember'
                    Severity  = 'Warning'
                    ViewModel = $vm.Name
                    Message   = "Member '$member' declared in ViewModel '$($vm.Name)' hides base member from AsyncViewModelBase without 'new'. Consider adding 'new' or refactoring to reuse base member."
                    File      = $vm.FilePath
                }
            }
        }

        # Report Prism MVVM usage
        if (-not $vm.UsesPrismMvvm) {
            $results += [PSCustomObject]@{
                Type      = 'MissingPrismMvvm'
                Severity  = 'Warning'
                ViewModel = $vm.Name
                Message   = 'ViewModel does not appear to use Prism.Mvvm patterns (BindableBase, RaisePropertyChanged). Consider migrating to Prism MVVM.'
                File      = $vm.FilePath
            }
        }

        if (-not $vm.UsesPrismCommands -and $vm.Commands.Count -gt 0) {
            $results += [PSCustomObject]@{
                Type      = 'MissingPrismCommands'
                Severity  = 'Warning'
                ViewModel = $vm.Name
                Message   = 'ViewModel has commands but does not appear to use Prism.Commands. Consider using DelegateCommand.'
                File      = $vm.FilePath
            }
        }

        if ($vm.LegacyIoC) {
            $results += [PSCustomObject]@{
                Type      = 'LegacyDI'
                Severity  = 'Warning'
                ViewModel = $vm.Name
                Message   = "Legacy IoC/DI usage detected: $($vm.LegacyIoC). Consider migrating DI to Prism/Unity patterns."
                File      = $vm.FilePath
            }
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
