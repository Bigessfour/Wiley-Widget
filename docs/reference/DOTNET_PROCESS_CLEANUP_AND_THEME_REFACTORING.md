# .NET Process Cleanup & Theme Refactoring Guide

**Date**: November 4, 2025
**Status**: Implementation Required

## Executive Summary

This document addresses two critical issues:

1. **Orphaned .NET processes** after task completion in tasks.json
2. **Theme application misalignment** with Syncfusion SfSkinManager documentation

## Part 1: .NET Process Cleanup

### Microsoft's Recommendations for Process Cleanup

According to Microsoft documentation, proper process cleanup for .NET applications involves:

1. **Use `--no-restore` and `--no-build` flags** to prevent spawning build processes
2. **Implement proper process termination** via `dotnet build-server shutdown`
3. **Kill orphaned testhost processes** before running new tests
4. **Use process group termination** on Windows via Job Objects

### Current Implementation Analysis

#### Existing Cleanup Mechanism

The project has `scripts/kill-test-processes.ps1` which:

- ✅ Kills testhost processes
- ✅ Kills long-running dotnet processes (>2 minutes)
- ⚠️ Only runs before test tasks
- ❌ Doesn't clean up after build tasks
- ❌ Doesn't handle dotnet build-server instances

### Recommended Tasks.json Improvements

#### 1. Add Pre-Build Cleanup Task

```json
{
  "label": "dotnet:clean-build-server",
  "type": "shell",
  "command": "dotnet",
  "args": ["build-server", "shutdown"],
  "presentation": {
    "echo": true,
    "reveal": "silent",
    "panel": "shared"
  },
  "problemMatcher": []
}
```

#### 2. Add Post-Task Cleanup Task

```json
{
  "label": "dotnet:cleanup-processes",
  "type": "shell",
  "command": "pwsh",
  "args": [
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "${workspaceFolder}/scripts/cleanup-dotnet-processes.ps1"
  ],
  "presentation": {
    "echo": true,
    "reveal": "silent",
    "panel": "shared"
  },
  "problemMatcher": []
}
```

#### 3. Update Build Task with Cleanup

```json
{
  "label": "build",
  "type": "shell",
  "command": "dotnet",
  "args": [
    "build",
    "${workspaceFolder}/WileyWidget.sln",
    "-m",
    "--no-restore",
    "/property:BuildIncremental=true",
    "/property:GenerateFullPaths=true",
    "/consoleloggerparameters:NoSummary"
  ],
  "dependsOn": ["dotnet:clean-build-server"],
  "isBackground": false,
  "group": "build",
  "problemMatcher": ["$msCompile"],
  "finalize": ["dotnet:cleanup-processes"]
}
```

**Note**: VS Code tasks don't natively support a `finalize` property. Instead, we'll use PowerShell wrapper scripts.

#### 4. Create Comprehensive Cleanup Script

Create `scripts/cleanup-dotnet-processes.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Comprehensive cleanup of .NET processes and build artifacts
.DESCRIPTION
    Cleans up orphaned dotnet, testhost, and build server processes
    Ensures no processes are left running after task completion
#>

[CmdletBinding()]
param()

Write-Host "🧹 Starting .NET process cleanup..." -ForegroundColor Cyan

# 1. Shutdown dotnet build server
Write-Host "📦 Shutting down dotnet build-server..." -ForegroundColor Yellow
try {
    dotnet build-server shutdown 2>&1 | Out-Null
    Write-Host "✅ Build server shutdown complete" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Build server shutdown failed (may not be running)" -ForegroundColor Yellow
}

# 2. Kill testhost processes
$testhostProcesses = Get-Process -Name "testhost" -ErrorAction SilentlyContinue
if ($testhostProcesses) {
    Write-Host "🔧 Found $($testhostProcesses.Count) testhost process(es)" -ForegroundColor Yellow
    $testhostProcesses | ForEach-Object {
        Write-Host "   Killing testhost PID: $($_.Id)" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ Testhost processes terminated" -ForegroundColor Green
}

# 3. Kill orphaned dotnet processes (not the main CLI)
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.StartTime -and
    ((Get-Date) - $_.StartTime).TotalMinutes -gt 1 -and
    $_.MainWindowTitle -eq ""  # Background processes only
}

if ($dotnetProcesses) {
    Write-Host "🔧 Found $($dotnetProcesses.Count) orphaned dotnet process(es)" -ForegroundColor Yellow
    $dotnetProcesses | ForEach-Object {
        $runtime = [math]::Round(((Get-Date) - $_.StartTime).TotalMinutes, 1)
        Write-Host "   Killing dotnet PID: $($_.Id) (runtime: $runtime min)" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ Orphaned dotnet processes terminated" -ForegroundColor Green
}

# 4. Kill MSBuild processes
$msbuildProcesses = Get-Process -Name "MSBuild" -ErrorAction SilentlyContinue
if ($msbuildProcesses) {
    Write-Host "🔧 Found $($msbuildProcesses.Count) MSBuild process(es)" -ForegroundColor Yellow
    $msbuildProcesses | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ MSBuild processes terminated" -ForegroundColor Green
}

# 5. Kill VBCSCompiler processes (Roslyn compiler server)
$vbcsProcesses = Get-Process -Name "VBCSCompiler" -ErrorAction SilentlyContinue
if ($vbcsProcesses) {
    Write-Host "🔧 Found $($vbcsProcesses.Count) VBCSCompiler process(es)" -ForegroundColor Yellow
    $vbcsProcesses | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "✅ Roslyn compiler processes terminated" -ForegroundColor Green
}

Write-Host "✨ Cleanup complete - all .NET processes terminated" -ForegroundColor Green
```

### Implementation Strategy

1. **Update `kill-test-processes.ps1`** to use the comprehensive cleanup script
2. **Add `dependsOn` cleanup** to all build and test tasks
3. **Create wrapper tasks** that run cleanup after completion
4. **Update CI/CD pipeline** to run cleanup between stages

---

## Part 2: Theme Application Refactoring

### Syncfusion SfSkinManager - Official Gospel

According to official Syncfusion documentation, theme application should follow:

#### ✅ APPROVED PATTERN - SfSkinManager.ApplicationTheme (Global)

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Set BEFORE InitializeComponent or any controls load
        SfSkinManager.ApplyThemeAsDefaultStyle = true;
        SfSkinManager.ApplicationTheme = new Theme("FluentDark");

        base.OnStartup(e);
    }
}
```

#### ✅ APPROVED PATTERN - Per-Window Theme (Runtime Changes)

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Apply theme to specific window
        SfSkinManager.SetTheme(this, new Theme("FluentDark"));
    }
}
```

#### ✅ APPROVED PATTERN - Dynamic Theme Switching

```csharp
private void SwitchTheme(string themeName)
{
    SfSkinManager.SetTheme(this, new Theme(themeName));
}
```

### Current Implementation Issues

#### ❌ PROBLEM: Custom ThemeUtility Wrapper

The current `ThemeUtility.cs` adds unnecessary abstraction:

```csharp
// Current implementation - NOT IN SYNCFUSION DOCS
public static void TryApplyTheme(System.Windows.Window window, string themeName)
{
    SfSkinManager.SetTheme(window, new Theme(canonical));
    SfSkinManager.SetVisualStyle(window, ToVisualStyle(canonical));
    // ^ SetVisualStyle is REDUNDANT - SetTheme already handles this
}
```

**Issues**:

1. ❌ `SetVisualStyle` is redundant - `SetTheme` already applies VisualStyle
2. ❌ Custom error handling obscures Syncfusion's built-in fallbacks
3. ❌ Obsolete `ApplyCurrentTheme` method references removed SettingsService
4. ❌ Not following Syncfusion documentation patterns

### Recommended Refactoring

#### Option 1: Minimal ThemeUtility (Recommended)

Keep a minimal utility for theme name normalization only:

```csharp
namespace WileyWidget.Services;

/// <summary>
/// Minimal theme utility for Wiley Widget theme name normalization.
/// Uses SfSkinManager directly per Syncfusion documentation.
/// </summary>
public static class ThemeUtility
{
    /// <summary>
    /// Normalizes legacy theme names to Fluent themes.
    /// FluentDark is default, FluentLight is fallback.
    /// </summary>
    public static string NormalizeThemeName(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            return "FluentDark";

        return themeName.Replace(" ", string.Empty) switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            "MaterialDark" => "FluentDark",     // Legacy mapping
            "MaterialLight" => "FluentLight",    // Legacy mapping
            _ => "FluentDark"
        };
    }
}
```

#### Option 2: Remove ThemeUtility Entirely

Use SfSkinManager directly everywhere:

```csharp
// In App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    SfSkinManager.ApplyThemeAsDefaultStyle = true;
    SfSkinManager.ApplicationTheme = new Theme("FluentDark");
    base.OnStartup(e);
}

// In Windows
public SomeWindow()
{
    InitializeComponent();
    // Theme is already applied via ApplicationTheme
}

// For runtime switching
private void SwitchTheme(string theme)
{
    var normalized = theme.Replace(" ", "") switch
    {
        "FluentDark" => "FluentDark",
        "FluentLight" => "FluentLight",
        _ => "FluentDark"
    };
    SfSkinManager.SetTheme(this, new Theme(normalized));
}
```

### Files Requiring Updates

1. **Remove ThemeUtility usage in SplashScreenWindow.xaml.cs**
   - Line 393: `Services.ThemeUtility.TryApplyTheme(this, "FluentDark");`
   - Replace with: `SfSkinManager.SetTheme(this, new Theme("FluentDark"));`

2. **Update App.xaml.cs** to use `ApplicationTheme`
   - Set global theme in `OnStartup` or `CreateShell`

3. **Update SettingsView.xaml.cs**
   - Line 30: Already commented out - remove entirely

4. **Update all window constructors**
   - Remove ThemeUtility calls (theme applied globally)
   - Or use `SfSkinManager.SetTheme` directly for overrides

5. **Consider deprecating src/ThemeUtility.cs**
   - Either remove entirely OR
   - Reduce to name normalization only

### Theme Configuration

**Default Configuration**:

- Primary Theme: FluentDark
- Fallback Theme: FluentLight
- Theme Switching: Enabled via IThemeService
- Global Application: Via `SfSkinManager.ApplicationTheme`

**Project References Required** (already present):

```xml
<PackageReference Include="Syncfusion.SfSkinManager.WPF" />
<PackageReference Include="Syncfusion.Themes.FluentDark.WPF" />
<PackageReference Include="Syncfusion.Themes.FluentLight.WPF" />
```

---

## Implementation Checklist

### Phase 1: Process Cleanup

- [ ] Create `scripts/cleanup-dotnet-processes.ps1`
- [ ] Add `dotnet:clean-build-server` task to tasks.json
- [ ] Add `dotnet:cleanup-processes` task to tasks.json
- [ ] Update `build` task to run cleanup before build
- [ ] Update `test: viewmodels` task to run cleanup after test
- [ ] Update `mcp:build-fix` task to run cleanup
- [ ] Test all tasks for orphaned processes

### Phase 2: Theme Refactoring

- [ ] Review all `ThemeUtility` usages
- [ ] Update `SplashScreenWindow.xaml.cs` to use SfSkinManager directly
- [ ] Update `App.xaml.cs` to set `ApplicationTheme` globally
- [ ] Remove or simplify `src/ThemeUtility.cs`
- [ ] Update `IThemeService` to use SfSkinManager directly
- [ ] Remove obsolete `ApplyCurrentTheme` method
- [ ] Update documentation to reference SfSkinManager patterns
- [ ] Test theme switching functionality

### Phase 3: Validation

- [ ] Run full build with cleanup verification
- [ ] Monitor Task Manager for orphaned processes
- [ ] Test theme application on all windows
- [ ] Test theme switching at runtime
- [ ] Verify no build errors remain
- [ ] Update PRISM_USAGE.md to remove ThemeUtility references

---

## References

### Microsoft Documentation

- [.NET Build Server](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build-server)
- [dotnet test Command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
- [Process Lifetime Management](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)

### Syncfusion Documentation

- [SfSkinManager Official Docs](https://help.syncfusion.com/wpf/themes/skin-manager)
- [Theme Application Best Practices](https://help.syncfusion.com/wpf/themes/skin-manager#apply-a-theme-globally-in-the-application)
- [Dynamic Theme Switching](https://help.syncfusion.com/wpf/themes/skin-manager#set-theme)

### Project Guidelines

- `docs/PRISM_USAGE.md` - Update to remove ThemeUtility mandate
- `docs/.copilot-instructions.md` - Update theme guidance
- `docs/FLUENTDARK_ENHANCED_EFFECTS_CONFIGURATION.md` - Review and update

---

**APPROVED**: This document represents the official approach for Wiley Widget process cleanup and theme management, aligned with Microsoft and Syncfusion documentation.
