# XAML Parsing and Markup Exception Handling Guide

## Overview

This guide provides comprehensive solutions for resolving `System.Windows.Markup.XamlParseException` and `System.Xaml.XamlObjectWriterException` in WPF Prism applications using Syncfusion controls.

## Common Exception Types

### System.Windows.Markup.XamlParseException

- **Cause**: XAML compilation or parsing failures
- **Common Triggers**: Missing assemblies, invalid xmlns declarations, ViewModelLocator errors
- **Inner Exception**: Often contains the root cause

### System.Xaml.XamlObjectWriterException

- **Cause**: Issues during XAML object graph construction
- **Common Triggers**: Type resolution failures, property binding errors, resource dictionary issues

## Diagnostic Tools

### 1. Binary Logging for XAML Compilation

Enable detailed MSBuild diagnostics:

```powershell
# Set binary log path for detailed XAML compilation diagnostics
$env:MSBUILDDEBUGPATH = "C:\Temp\XamlDebug"
dotnet build /bl:"xaml-debug.binlog"
```

### 2. WPF Trace Sources

Enable comprehensive WPF tracing:

```csharp
// In App.xaml.cs OnStartup
System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.All;
System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new System.Diagnostics.DefaultTraceListener());

System.Diagnostics.PresentationTraceSources.MarkupSource.Switch.Level = System.Diagnostics.SourceLevels.All;
System.Diagnostics.PresentationTraceSources.MarkupSource.Listeners.Add(new System.Diagnostics.DefaultTraceListener());
```

### 3. XAML Validation Tool

```csharp
public static class XamlValidator
{
    public static void ValidateXamlFile(string xamlPath)
    {
        try
        {
            using (var stream = File.OpenRead(xamlPath))
            {
                System.Windows.Markup.XamlReader.Load(stream);
            }
            Log.Information("XAML validation passed: {Path}", xamlPath);
        }
        catch (XamlParseException ex)
        {
            Log.Error(ex, "XAML validation failed: {Path}", xamlPath);
            Log.Error("Line: {Line}, Position: {Position}", ex.LineNumber, ex.LinePosition);
            throw;
        }
    }
}
```

## Required NuGet Packages

### Core WPF Dependencies

```xml
<PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.39" />
<PackageReference Include="Prism.Core" Version="9.0.537" />

<PackageReference Include="Prism.Container.DryIoc" Version="9.0.107" />
```

### Syncfusion Dependencies

```xml
<PackageReference Include="Syncfusion.Licensing" Version="25.1.39" />
<PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="25.1.39" />
<PackageReference Include="Syncfusion.SfGrid.WPF" Version="25.1.39" />
<PackageReference Include="Syncfusion.Themes.FluentLight.WPF" Version="25.1.39" />
<PackageReference Include="Syncfusion.Shared.WPF" Version="25.1.39" />
```

## Syncfusion License Management

### Environment Variable Setup

```powershell
# Set Syncfusion license key
$env:SyncfusionLicense = "Your_License_Key_Here"

# Or use machine-level environment variable
[Environment]::SetEnvironmentVariable("SyncfusionLicense", "Your_License_Key_Here", "Machine")
```

### License Registration in Code

```csharp
// In App.xaml.cs OnStartup, before any Syncfusion controls are used
private void EnsureSyncfusionLicenseRegistered()
{
    try
    {
        string licenseKey = Configuration["SyncfusionLicense"] ??
                           Environment.GetEnvironmentVariable("SyncfusionLicense");

        if (!string.IsNullOrEmpty(licenseKey))
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            Log.Information("Syncfusion license registered successfully");
        }
        else
        {
            Log.Warning("Syncfusion license key not found. Set 'SyncfusionLicense' in appsettings.json or environment variable");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to register Syncfusion license");
        // Continue startup - license dialog will appear if needed
    }
}
```

## xmlns Declaration Validation

### Common xmlns Issues

1. **Missing Syncfusion xmlns**
2. **Incorrect Prism xmlns**
3. **Missing Behaviors xmlns**

### Correct xmlns Declarations

```xml
<Window x:Class="WileyWidget.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"

        xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
        xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
        xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
        xmlns:behaviors="clr-namespace:WileyWidget.Behaviors"
        xmlns:vm="clr-namespace:WileyWidget.ViewModels"
        prism:ViewModelLocator.AutoWireViewModel="True">
```

## ViewModelLocator Configuration

### Prism ViewModelLocator Issues

- **AutoWireViewModel="True"** not set
- **ViewModel namespace mismatch**
- **Missing ViewModel constructor**

### ViewModelLocator Validation

```csharp
public static class ViewModelLocatorValidator
{
    public static void ValidateViewModelLocator(FrameworkElement view)
    {
        try
        {
            var viewModel = view.DataContext;
            if (viewModel == null)
            {
                Log.Warning("ViewModelLocator failed for {ViewType}", view.GetType().Name);
                Log.Warning("Check that ViewModelLocator.AutoWireViewModel='True' and ViewModel exists");
            }
            else
            {
                Log.Debug("ViewModelLocator resolved {ViewModelType} for {ViewType}",
                    viewModel.GetType().Name, view.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ViewModelLocator validation failed for {ViewType}", view.GetType().Name);
        }
    }
}
```

## SfDataGrid Specific Issues

### Common SfDataGrid Problems

1. **Missing SfSkinManager theme application**
2. **Incorrect column definitions**
3. **Data binding issues**

### SfDataGrid Best Practices

```xml
<!-- Correct SfDataGrid usage -->
<syncfusion:SfDataGrid ItemsSource="{Binding DataItems}"
                      AutoGenerateColumns="False"
                      syncfusionskin:SfSkinManager.VisualStyle="FluentLight">
    <syncfusion:SfDataGrid.Columns>
        <syncfusion:GridTextColumn MappingName="Name" HeaderText="Name" />
        <syncfusion:GridNumericColumn MappingName="Value" HeaderText="Value" />
    </syncfusion:SfDataGrid.Columns>
</syncfusion:SfDataGrid>
```

### Prism Region Interaction and InvalidCastException

A common runtime error is System.InvalidCastException coming from Prism when a Syncfusion control (often `SfDataGrid`) is inadvertently used as a region host. Prism expects region hosts to be compatible with `ItemsControl`, `ContentControl` or `Selector` semantics. `SfDataGrid` is a specialized control for tabular data and is not a drop-in region host. When Prism attempts to adapt a control it doesn't fully support, internal casts can fail and surface as `InvalidCastException` in Prism.dll.

Resolution steps:

- Avoid calling `regionManager.RegisterViewWithRegion("MyRegion", typeof(MyView))` where the target control is an `SfDataGrid` instance. Instead use a `ContentControl` or `ItemsControl` in XAML for navigation targets and keep `SfDataGrid` inside your view.
- If you must bridge Prism regions to Syncfusion controls, implement a custom region adapter that maps views to the grid's data items (not UIElement insertion). The repository includes a protective `SfDataGridRegionAdapter` which throws a clear `InvalidOperationException` explaining the correct patterns rather than letting Prism throw an opaque `InvalidCastException`.
- Example of safe pattern:
  - Create a view (e.g., `CustomersView`) that contains an `SfDataGrid` bound to a viewmodel collection.
  - Register `CustomersView` for navigation (e.g., `containerRegistry.RegisterForNavigation<CustomersView, CustomersViewModel>()`).
  - Navigate to `CustomersView` in a `ContentControl` region. The grid remains internal to the view and will not be used as a region host.

See `src/Regions/SfDataGridRegionAdapter.cs` for the protective adapter and `XAML_EXCEPTION_HANDLING_GUIDE.md` for examples.

## Exception Recovery Strategies

### Global Exception Handler

```csharp
private void SetupGlobalExceptionHandling()
{
    // Handle XAML parsing exceptions
    Application.Current.DispatcherUnhandledException += (sender, e) =>
    {
        if (e.Exception is XamlParseException xamlEx)
        {
            Log.Error(xamlEx, "XAML Parse Exception: {Message}", xamlEx.Message);
            Log.Error("File: {File}, Line: {Line}, Position: {Position}",
                xamlEx.SourceUri?.ToString() ?? "Unknown",
                xamlEx.LineNumber,
                xamlEx.LinePosition);

            // Attempt recovery for known issues
            if (TryRecoverFromXamlException(xamlEx))
            {
                e.Handled = true;
                return;
            }
        }

        // Log and allow default handling for other exceptions
        Log.Error(e.Exception, "Unhandled exception");
    };
}

private bool TryRecoverFromXamlException(XamlParseException ex)
{
    string message = ex.Message.ToLowerInvariant();

    // Check for common recoverable issues
    if (message.Contains("syncfusion") && message.Contains("license"))
    {
        Log.Warning("Attempting Syncfusion license recovery");
        EnsureSyncfusionLicenseRegistered();
        return true;
    }

    if (message.Contains("viewmodellocator"))
    {
        Log.Warning("ViewModelLocator issue detected - check ViewModel registration");
        return false; // Requires code fix
    }

    return false;
}
```

## Debugging Checklist

### Pre-Build Validation

1. ✅ Verify all required NuGet packages are installed
2. ✅ Check Syncfusion license is configured
3. ✅ Validate xmlns declarations in XAML files
4. ✅ Ensure ViewModelLocator.AutoWireViewModel="True"
5. ✅ Verify SfSkinManager theme is applied globally

### Runtime Diagnostics

1. ✅ Enable WPF trace sources
2. ✅ Check binary logs for compilation issues
3. ✅ Validate ViewModel resolution
4. ✅ Monitor Syncfusion license status
5. ✅ Review XAML binding errors

### Build Configuration

```xml
<!-- In .csproj for better XAML diagnostics -->
<PropertyGroup>
  <XamlDebuggingInformation>true</XamlDebuggingInformation>
  <DebugType>full</DebugType>
  <DebugSymbols>true</DebugSymbols>
</PropertyGroup>
```

## Troubleshooting Commands

### Build with Diagnostics

```powershell
# Build with detailed logging
dotnet build /verbosity:detailed /flp:LogFile=build.log;verbosity=diagnostic

# Build with binary logging
dotnet build /bl:xaml-debug.binlog
```

### Runtime Debugging

```powershell
# Enable WPF diagnostics
$env:ENABLE_XAML_DIAGNOSTICS = "1"
$env:WPF_TRACE_SETTINGS = "DataBinding:All;Markup:All"

# Run with diagnostics
dotnet run
```

### Log Analysis

```powershell
# Search for XAML errors in logs
Select-String -Path "*.log" -Pattern "XamlParseException|XamlObjectWriterException" -Context 3
```

## Prevention Best Practices

1. **Always apply SfSkinManager theme globally before loading XAML**
2. **Validate XAML files during build process**
3. **Use proper xmlns declarations consistently**
4. **Ensure ViewModelLocator configuration is correct**
5. **Keep Syncfusion packages updated and compatible**
6. **Enable comprehensive logging for production troubleshooting**

## Emergency Recovery

If XAML parsing fails completely:

1. **Disable problematic modules temporarily**
2. **Use fallback themes (remove Syncfusion dependencies)**
3. **Implement graceful degradation for missing controls**
4. **Provide clear error messages to users**
5. **Log detailed diagnostic information for support**

This guide should resolve most XAML parsing and markup exceptions in WPF Prism applications with Syncfusion controls.
