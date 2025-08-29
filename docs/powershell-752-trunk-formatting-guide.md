# PowerShell 7.5.2 Formatting with Trunk CI/CD

## Overview

This project uses Trunk CI/CD with enhanced PowerShell 7.5.2 formatting rules to ensure consistent code quality and adherence to Microsoft best practices.

## Configuration Files

### 1. PSScriptAnalyzer Settings (`.trunk/configs/PSScriptAnalyzerSettings.psd1`)

Comprehensive rules for PowerShell 7.5.2 compliance:

```powershell
# Core formatting rules
PSPlaceOpenBrace           # Enforce proper brace placement
PSPlaceCloseBrace          # Enforce proper brace placement
PSUseConsistentIndentation # 4-space indentation
PSUseConsistentWhitespace  # Consistent spacing
PSAlignAssignmentStatement # Align assignment operators

# PowerShell 7.5.2 compatibility
PSUseCompatibleSyntax      # 7.5.2 syntax compliance
PSUseCompatibleCmdlets     # 7.5.2 cmdlet compatibility
PSUseCompatibleTypes       # 7.5.2 type compatibility

# Security and best practices
PSAvoidUsingWriteHost      # STRICT: No Write-Host allowed
PSUseApprovedVerbs         # Approved cmdlet verbs only
PSAvoidUsingCmdletAliases  # Use full cmdlet names
```

### 2. Trunk Configuration (`.trunk/trunk.yaml`)

```yaml
lint:
  enabled:
    - psscriptanalyzer@1.24.0:
        config: .trunk/configs/PSScriptAnalyzerSettings.psd1

tools:
  enabled:
    - pwsh@7.5.2  # PowerShell 7.5.2 runtime
```

## PowerShell 7.5.2 Formatting Standards

### ✅ Correct Formatting Examples

#### Function Definition
```powershell
function Get-UserData {
    param(
        [Parameter(Mandatory = $true)]
        [string]$UserName,

        [Parameter(Mandatory = $false)]
        [switch]$IncludeInactive
    )

    try {
        $user = Get-ADUser -Identity $UserName -Properties *

        if ($IncludeInactive) {
            return $user
        }
        else {
            return $user | Where-Object { $_.Enabled -eq $true }
        }
    }
    catch {
        Write-Error "Failed to get user data: $_"
        return $null
    }
}
```

#### Pipeline Usage
```powershell
Get-Process |
    Where-Object { $_.CPU -gt 10 } |
    Sort-Object -Property CPU -Descending |
    Select-Object -First 5 |
    Format-Table -AutoSize
```

#### Error Handling
```powershell
try {
    $data = Get-Content -Path $configPath -Raw |
            ConvertFrom-Json
    Write-Information "Configuration loaded successfully" -InformationAction Continue
}
catch {
    Write-Error "Failed to load configuration: $_"
    throw
}
```

### ❌ Incorrect Formatting (Will Be Flagged)

#### Wrong Brace Placement
```powershell
# ❌ Wrong
function Test-Function{ Write-Host "Hello" }

# ✅ Correct
function Test-Function {
    Write-Output "Hello"
}
```

#### Inconsistent Indentation
```powershell
# ❌ Wrong (mixed tabs and spaces)
function Test-Function {
	Write-Host "Tab indentation"  # Tab character
    Write-Host "Space indentation"  # Spaces
}

# ✅ Correct (4 spaces only)
function Test-Function {
    Write-Output "Consistent 4-space indentation"
}
```

#### Write-Host Usage
```powershell
# ❌ Wrong
Write-Host "Processing..." -ForegroundColor Green

# ✅ Correct
Write-Information "Processing..." -InformationAction Continue
```

## Using the Formatter

### Manual Formatting

```powershell
# Check formatting without fixing
.\scripts\Format-PowerShell-Trunk.ps1 -CheckOnly

# Fix formatting issues
.\scripts\Format-PowerShell-Trunk.ps1 -Fix

# Format specific paths
.\scripts\Format-PowerShell-Trunk.ps1 -Path "scripts", "src" -Fix
```

### Trunk CI/CD Integration

Trunk will automatically run PSScriptAnalyzer with the custom configuration:

```bash
# Check all files
trunk check

# Fix formatting issues
trunk fmt

# Check specific files
trunk check --filter=psscriptanalyzer
```

### VS Code Integration

The `.vscode/settings.json` is configured for real-time formatting:

```json
{
  "powershell.scriptAnalysis.enable": true,
  "powershell.scriptAnalysis.runOnSave": true,
  "powershell.scriptAnalysis.runOnOpen": true,
  "powershell.codeFormatting.preset": "OTBS",
  "powershell.codeFormatting.autoCorrectAliases": true,
  "powershell.codeFormatting.useCorrectCasing": true,
  "[powershell]": {
    "editor.defaultFormatter": "ms-vscode.powershell",
    "editor.formatOnSave": true,
    "editor.formatOnType": true
  }
}
```

## Rule Enforcement Levels

### Error Level (Must Fix)
- `PSPlaceOpenBrace` - Incorrect brace placement
- `PSPlaceCloseBrace` - Incorrect brace placement
- `PSUseConsistentIndentation` - Wrong indentation
- `PSAvoidUsingWriteHost` - Write-Host usage
- `PSUseCompatibleSyntax` - Incompatible syntax
- `PSUseCompatibleCmdlets` - Incompatible cmdlets

### Warning Level (Should Fix)
- `PSAlignAssignmentStatement` - Misaligned assignments
- `PSUseApprovedVerbs` - Unapproved verbs
- `PSAvoidUsingCmdletAliases` - Cmdlet aliases
- `PSUseConsistentWhitespace` - Inconsistent spacing

## PowerShell 7.5.2 Features to Use

### Modern Syntax
```powershell
# Ternary operator
$result = $condition ? $value1 : $value2

# Null-coalescing assignment
$user ??= Get-CurrentUser

# Pipeline parallelization
$data | ForEach-Object -Parallel {
    Process-Item $_
}
```

### Proper Output Methods
```powershell
# Data output (pipeline compatible)
Write-Output $data

# User information
Write-Information "Starting process..." -InformationAction Continue

# Debug information
Write-Verbose "Processing item: $item" -Verbose:$VerbosePreference

# Warnings
Write-Warning "Configuration file not found"

# Errors
Write-Error "Operation failed: $_"
```

## Troubleshooting

### Common Issues

#### PSScriptAnalyzer Not Running
```powershell
# Check if module is installed
Get-Module PSScriptAnalyzer -ListAvailable

# Install if missing
Install-Module PSScriptAnalyzer -Force
```

#### Formatting Not Applied
```powershell
# Force reload VS Code PowerShell session
# Ctrl+Shift+P → "PowerShell: Restart Current Session"
```

#### Trunk Configuration Issues
```bash
# Validate trunk.yaml
trunk check --validate

# Debug specific linter
trunk check --filter=psscriptanalyzer --verbose
```

### VS Code Extensions Required

```vscode-extensions
ms-vscode.powershell              # PowerShell language support
pspester.pester-test             # Testing framework
ms-dotnettools.dotnet-interactive-vscode  # .NET Interactive
```

## Integration with CI/CD

### GitHub Actions Example
```yaml
- name: Run Trunk Check
  uses: trunk-io/trunk-action@v1
  with:
    check-mode: all
    arguments: --filter=psscriptanalyzer

- name: Format PowerShell Files
  run: .\scripts\Format-PowerShell-Trunk.ps1 -Fix
  shell: pwsh
```

### Azure DevOps Example
```yaml
- task: PowerShell@2
  displayName: 'Check PowerShell Formatting'
  inputs:
    targetType: 'inline'
    script: '.\scripts\Format-PowerShell-Trunk.ps1 -CheckOnly'
    errorActionPreference: 'stop'

- task: PowerShell@2
  displayName: 'Format PowerShell Files'
  inputs:
    targetType: 'inline'
    script: '.\scripts\Format-PowerShell-Trunk.ps1 -Fix'
```

## Best Practices

1. **Always run formatting before commit**
2. **Use approved PowerShell verbs**
3. **Avoid Write-Host in production code**
4. **Use proper error handling patterns**
5. **Follow consistent naming conventions**
6. **Test scripts with Pester**
7. **Document functions with comment-based help**

## References

- [Microsoft PowerShell Documentation](https://docs.microsoft.com/en-us/powershell/)
- [PSScriptAnalyzer GitHub](https://github.com/PowerShell/PSScriptAnalyzer)
- [PowerShell 7.5.2 Release Notes](https://docs.microsoft.com/en-us/powershell/scripting/whats-new/what-s-new-in-powershell-75)
- [Trunk CI/CD Documentation](https://docs.trunk.io/)
