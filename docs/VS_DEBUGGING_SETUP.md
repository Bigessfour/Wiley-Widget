# Visual Studio Debugging Setup for WileyWidget

## XAML Parse Exception Debugging

### Enable XamlParseException Breakpoints

1. **Open Visual Studio** and load the WileyWidget solution
2. **Go to Debug → Exceptions** (or press `Ctrl+Alt+E`)
3. **Expand "Common Language Runtime Exceptions"**
4. **Find and check the box for:**
   - `System.Windows.Markup.XamlParseException`
5. **Click "OK" to save**

### Just My Code Settings

1. **Go to Tools → Options**
2. **Navigate to Debugging → General**
3. **Uncheck "Enable Just My Code"**
4. **Check "Enable .NET Framework source stepping"** (if available)
5. **Click "OK" to save**

### Attach to Process for WPF Debugging

When debugging WPF startup issues:

1. **Start the application without debugging** (`Ctrl+F5`)
2. **Go to Debug → Attach to Process** (or press `Ctrl+Alt+P`)
3. **Find the WileyWidget process** in the list
4. **Select it and click "Attach"**
5. **Set breakpoints in:**
   - `App.xaml.cs` (Application_Startup)
   - `MainWindow.xaml.cs` (constructor)
   - Any XAML code-behind files

### WPF Tracing Configuration

The application is configured with detailed WPF tracing in `App.config`:

```xml
<system.diagnostics>
  <sources>
    <source name="System.Windows.Markup" switchValue="Warning,ActivityTracing">
      <listeners><add name="textListener"/></listeners>
    </source>
  </sources>
  <trace autoflush="true"/>
</system.diagnostics>
```

This will log font and XAML issues to the Output window during debugging.

### Font and Rendering Issues

If experiencing font-related XAML parse exceptions:

1. **Check Output window** for WPF tracing messages
2. **Verify font availability** in Windows Fonts folder
3. **Check Docker font mounting** if using containerized development
4. **Enable software rendering** by setting environment variable:
   ```
   WPF_DISABLE_HW_ACCELERATION=true
   ```

### Common Breakpoints for XAML Issues

Set breakpoints in:
- `InitializeComponent()` methods
- Font loading code
- Resource dictionary loading
- Control template application

### Performance Debugging

For startup performance issues:
1. **Use Performance Profiler** (Debug → Performance Profiler)
2. **Enable WPF tracing** as configured
3. **Check Timeline** for UI thread blocking
4. **Monitor font loading times** in Output window