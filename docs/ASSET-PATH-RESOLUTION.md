# Asset Path Resolution Guide - WileyWidget

## 📋 Overview

This document explains how to correctly reference image assets (SVG, PNG, etc.) in WinUI 3 XAML views to ensure they display properly in previews and at runtime.

## 🗂️ Asset Directory Structure

```
src/WileyWidget.WinUI/Assets/
├── README.md
├── Icons/
│   ├── icon.svg
│   └── icon_foreground.svg
└── Splash/
    └── splash_screen.svg
```

## 🔗 Path Resolution Formats

### ✅ Correct Formats for WinUI 3

| Format | Use Case | Example |
|--------|----------|---------|
| `ms-appx:///Assets/...` | **PREFERRED** - Absolute app package path | `ms-appx:///Assets/Icons/icon.svg` |
| `/Assets/...` | Relative to app root | `/Assets/Icons/icon.svg` |
| `Assets/...` | Relative to current assembly | `Assets/Icons/icon.svg` |

### ❌ Formats to Avoid

| Format | Why Avoid | Issue |
|--------|-----------|-------|
| `../Assets/...` | Relative navigation | Breaks in different contexts |
| `C:\Users\...\Assets\...` | Absolute file paths | Not portable, won't work in package |
| `file:///...` | File scheme | Security restrictions in UWP/WinUI |

## 📝 XAML Examples

### Basic Image Reference

```xml
<!-- SVG Image -->
<Image Source="ms-appx:///Assets/Icons/icon.svg" 
       Width="32" 
       Height="32" />

<!-- PNG Image with Scale Support -->
<Image Source="ms-appx:///Assets/Images/logo.png" 
       Width="64" 
       Height="64" />
```

### Image in Button

```xml
<Button>
    <Button.Content>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Image Source="ms-appx:///Assets/Icons/icon.svg" 
                   Width="16" 
                   Height="16" />
            <TextBlock Text="Click Me" />
        </StackPanel>
    </Button.Content>
</Button>
```

### Image with Fallback

```xml
<Image Width="48" Height="48">
    <Image.Source>
        <BitmapImage UriSource="ms-appx:///Assets/Icons/icon.svg" 
                     DecodePixelWidth="48" 
                     DecodePixelHeight="48" />
    </Image.Source>
</Image>
```

### ImageIcon (for NavigationView)

```xml
<NavigationViewItem Content="Dashboard">
    <NavigationViewItem.Icon>
        <ImageIcon Source="ms-appx:///Assets/Icons/dashboard.svg" />
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

## 🏗️ Project Configuration

### .csproj Configuration (REQUIRED)

Ensure your `WileyWidget.WinUI.csproj` includes:

```xml
<ItemGroup>
    <Content Include="Assets\**\*">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

### Build Action Settings

| File Type | Build Action | Copy to Output |
|-----------|--------------|----------------|
| `.svg` | Content | PreserveNewest |
| `.png` | Content | PreserveNewest |
| `.jpg` | Content | PreserveNewest |
| `.ico` | Content | PreserveNewest |

## 🔍 Troubleshooting

### Issue: Images Don't Display in Preview

**Symptoms:**
- Images show as blank in XAML designer
- "Asset not found" errors in Output window
- Images work at runtime but not in preview

**Solutions:**

1. **Verify Asset Exists:**
   ```powershell
   Test-Path "src\WileyWidget.WinUI\Assets\Icons\icon.svg"
   ```

2. **Check .csproj Configuration:**
   ```powershell
   Select-String -Path "src\WileyWidget.WinUI\WileyWidget.WinUI.csproj" -Pattern "Assets"
   ```

3. **Rebuild Project:**
   ```powershell
   dotnet clean
   dotnet build
   ```

4. **Clear Designer Cache:**
   - Close Visual Studio / VS Code
   - Delete `obj/` and `bin/` folders
   - Reopen and rebuild

### Issue: Images Display in Preview but Not at Runtime

**Symptoms:**
- XAML designer shows images correctly
- Runtime shows blank images or crashes

**Solutions:**

1. **Verify CopyToOutputDirectory:**
   ```xml
   <Content Include="Assets\**\*">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
   </Content>
   ```

2. **Check Output Directory:**
   ```powershell
   Get-ChildItem "src\WileyWidget.WinUI\bin\Debug\net9.0-windows10.0.26100.0\win-x64\Assets" -Recurse
   ```

3. **Verify URI Format:**
   - Use `ms-appx:///` prefix
   - Use forward slashes `/` or double backslashes `\\`
   - Ensure case-sensitive match on cross-platform builds

### Issue: SVG Files Not Rendering

**Symptoms:**
- PNG files work, but SVG files don't display
- SVG shows as blank box

**Solutions:**

1. **WinUI 3 SVG Support:**
   - WinUI 3 (Windows App SDK 1.8+) supports SVG natively
   - Ensure using Windows App SDK 1.8.3+

2. **Validate SVG Format:**
   ```powershell
   Get-Content "src\WileyWidget.WinUI\Assets\Icons\icon.svg" | Select-String "svg"
   ```

3. **SVG Compatibility:**
   - Ensure SVG is valid XML
   - Remove XML processing instructions if present
   - Use simple SVG features (complex filters may not render)

4. **Convert to PNG (Fallback):**
   ```powershell
   # Use online converter or tool like Inkscape
   # inkscape icon.svg --export-type=png --export-width=256
   ```

## 🧪 Testing Asset Paths

### Quick Test View

Add this to any XAML page to test all assets:

```xml
<StackPanel Orientation="Horizontal" Spacing="12" Margin="24">
    <TextBlock Text="Asset Test:" VerticalAlignment="Center" FontWeight="SemiBold" />
    
    <!-- Test: icon.svg -->
    <Border BorderBrush="{ThemeResource SystemAccentColor}" BorderThickness="1" Padding="8">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Image Source="ms-appx:///Assets/Icons/icon.svg" Width="24" Height="24" />
            <TextBlock Text="icon.svg" VerticalAlignment="Center" FontSize="12" />
        </StackPanel>
    </Border>
    
    <!-- Test: icon_foreground.svg -->
    <Border BorderBrush="{ThemeResource SystemAccentColor}" BorderThickness="1" Padding="8">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Image Source="ms-appx:///Assets/Icons/icon_foreground.svg" Width="24" Height="24" />
            <TextBlock Text="icon_foreground.svg" VerticalAlignment="Center" FontSize="12" />
        </StackPanel>
    </Border>
    
    <!-- Test: splash_screen.svg -->
    <Border BorderBrush="{ThemeResource SystemAccentColor}" BorderThickness="1" Padding="8">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Image Source="ms-appx:///Assets/Splash/splash_screen.svg" Width="24" Height="24" />
            <TextBlock Text="splash_screen.svg" VerticalAlignment="Center" FontSize="12" />
        </StackPanel>
    </Border>
</StackPanel>
```

### PowerShell Verification Script

```powershell
# verify-assets.ps1
param(
    [string]$ProjectPath = "src\WileyWidget.WinUI"
)

Write-Host "🔍 Verifying WinUI Asset Configuration" -ForegroundColor Cyan

# Check Assets directory exists
$assetsPath = Join-Path $ProjectPath "Assets"
if (Test-Path $assetsPath) {
    Write-Host "✅ Assets directory found: $assetsPath" -ForegroundColor Green
    
    # List all asset files
    Write-Host "`n📁 Asset Files:" -ForegroundColor Yellow
    Get-ChildItem $assetsPath -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Replace("$assetsPath\", "")
        $size = "{0:N2} KB" -f ($_.Length / 1KB)
        Write-Host "   $relativePath ($size)" -ForegroundColor Gray
    }
} else {
    Write-Host "❌ Assets directory not found: $assetsPath" -ForegroundColor Red
}

# Check .csproj configuration
$csprojPath = Join-Path $ProjectPath "WileyWidget.WinUI.csproj"
if (Test-Path $csprojPath) {
    $csprojContent = Get-Content $csprojPath -Raw
    
    if ($csprojContent -match '<Content Include="Assets\\\*\*\\\*">') {
        Write-Host "`n✅ .csproj includes Assets with wildcard pattern" -ForegroundColor Green
    } else {
        Write-Host "`n⚠️  .csproj may not include all Assets files" -ForegroundColor Yellow
    }
    
    if ($csprojContent -match 'CopyToOutputDirectory.*PreserveNewest') {
        Write-Host "✅ CopyToOutputDirectory is set to PreserveNewest" -ForegroundColor Green
    } else {
        Write-Host "❌ CopyToOutputDirectory not properly configured" -ForegroundColor Red
    }
}

# Check build output
$binPath = Join-Path $ProjectPath "bin\Debug\net9.0-windows10.0.26100.0\win-x64\Assets"
if (Test-Path $binPath) {
    Write-Host "`n✅ Assets found in build output: $binPath" -ForegroundColor Green
    $fileCount = (Get-ChildItem $binPath -Recurse -File).Count
    Write-Host "   $fileCount file(s) copied to output" -ForegroundColor Gray
} else {
    Write-Host "`n⚠️  Build output not found (project may need building)" -ForegroundColor Yellow
}

Write-Host "`n✅ Asset verification complete" -ForegroundColor Cyan
```

## 📚 References

- [Working with Assets - Uno Platform](https://platform.uno/docs/articles/features/working-with-assets.html)
- [Image and ImageBrush - WinUI 3](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.image)
- [Package a desktop app - Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/)
- [App resources and the Resource Management System](https://learn.microsoft.com/en-us/windows/uwp/app-resources/)

## 🔗 Related Documentation

- `.vscode/copilot-instructions.md` - MCP filesystem enforcement rules
- `src/WileyWidget.WinUI/Assets/README.md` - Asset requirements for packaging
- `src/WileyWidget.Uno/Assets/SharedAssets.md` - Uno Platform asset guidelines

---

**Last Updated:** November 23, 2025  
**Status:** ✅ Assets copied from Uno project, paths verified, test panel added to DashboardView
