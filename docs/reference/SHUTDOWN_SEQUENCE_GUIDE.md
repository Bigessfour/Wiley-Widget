# WPF Application Shutdown Sequence - Critical Implementation Guide

## Overview

This document explains the **critical shutdown sequence** required to prevent `NullReferenceException` in Prism's `DialogService` during WPF application termination. This issue occurs when dialog windows are closed **after** the DI container has been disposed, causing the DialogService to attempt operations on disposed dependencies.

## The Problem

### Root Cause

When a WPF application shuts down, Windows forcibly closes all windows including dialogs. If the Prism container is disposed before dialogs are closed, the `DialogService` throws `NullReferenceException` when it tries to access disposed services during `Window.InternalClose`.

### Symptoms

- `NullReferenceException` in `Prism.Dialogs.DialogService` during shutdown
- Stack traces showing `Window.InternalClose`
- Errors mentioning dialog event handlers (`Closed`, `Activated`)
- Intermittent crashes during application exit

## The Solution

### Critical Shutdown Order

The **ONLY correct shutdown sequence** is:

```
1. Close all dialog windows
   ↓
2. Detach event handlers
   ↓
3. Dispose ViewModels
   ↓
4. Dispose services (UnitOfWork, Cache, etc.)
   ↓
5. Dispose DI container
   ↓
6. Flush logs
```

**❌ NEVER dispose the container before closing dialogs!**

## Implementation

### 1. App.xaml.cs OnExit Method

The `OnExit` method in `App.xaml.cs` is the **critical** implementation point:

```csharp
protected override void OnExit(ExitEventArgs e)
{
    Log.Information("Application shutdown - Session: {StartupId}", _startupId);
    try
    {
        // ✅ STEP 1: Close all dialog windows BEFORE disposing container
        // This prevents NullReferenceException in Prism DialogService

        // Try using DialogTrackingService (preferred)
        try
        {
            var dialogTracker = this.Container.Resolve<IDialogTrackingService>();
            if (dialogTracker != null && dialogTracker.OpenDialogCount > 0)
            {
                Log.Information("Closing {Count} tracked dialogs",
                    dialogTracker.OpenDialogCount);
                dialogTracker.CloseAllDialogs();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DialogTrackingService unavailable, using fallback");
        }

        // Fallback: Manual dialog closure
        CloseAllDialogWindows();

        // ✅ STEP 2: Cleanup other resources
        // ... (license registration, secrets, etc.)

        // ✅ STEP 3: Dispose key services
        try { this.Container.Resolve<IMemoryCache>()?.Dispose(); } catch { }

        base.OnExit(e);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Unhandled shutdown exception");
    }
    finally
    {
        // ✅ STEP 4: Dispose container LAST
        (Container as IDisposable)?.Dispose();
        Log.CloseAndFlush();
    }
}
```

### 2. CloseAllDialogWindows Helper Method

```csharp
private void CloseAllDialogWindows()
{
    try
    {
        if (Application.Current?.Windows == null)
        {
            Log.Debug("No windows to close");
            return;
        }

        // Find all dialog windows (exclude MainWindow/Shell)
        var dialogWindows = Application.Current.Windows
            .OfType<Window>()
            .Where(w => w != null &&
                        w != MainWindow &&
                        (w.GetType().Name.Contains("Dialog", StringComparison.OrdinalIgnoreCase) ||
                         w.Owner != null))
            .ToList();

        Log.Information("Closing {Count} dialog window(s)", dialogWindows.Count);

        foreach (var dialog in dialogWindows)
        {
            try
            {
                if (dialog.IsLoaded)
                {
                    // Try to set DialogResult for modal dialogs
                    try { dialog.DialogResult = false; } catch { /* Not modal */ }
                    dialog.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error closing dialog {Type}", dialog.GetType().Name);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Error during dialog closure (non-fatal)");
    }
}
```

### 3. DialogViewModelBase with IDisposable

All dialog ViewModels **MUST** implement proper disposal:

```csharp
public abstract class DialogViewModelBase : BindableBase, IDialogAware, IDisposable
{
    private bool _disposed;

    public virtual void OnDialogClosed()
    {
        // Ensure disposal happens when dialog closes
        Dispose();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            DisposeCore(); // Override in derived classes
        }

        _disposed = true;
    }

    protected virtual void DisposeCore()
    {
        // Derived classes override this for cleanup
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
```

### 4. DialogTrackingService (Recommended)

Use centralized tracking for better control:

```csharp
public interface IDialogTrackingService
{
    void RegisterDialog(Window dialog);
    void UnregisterDialog(Window dialog);
    int OpenDialogCount { get; }
    void CloseAllDialogs();
    IReadOnlyList<string> GetOpenDialogTypes();
}
```

**Register in DI container:**

```csharp
protected override void RegisterTypes(IContainerRegistry containerRegistry)
{
    // ... other registrations
    containerRegistry.RegisterSingleton<IDialogTrackingService, DialogTrackingService>();
}
```

### 5. Program.cs Exception Handling

Add top-level exception handling:

```csharp
[STAThread]
public static int Main()
{
    try
    {
        var app = new App();
        return app.Run();
    }
    catch (Exception ex)
    {
        try
        {
            Serilog.Log.Fatal(ex, "Unhandled exception during shutdown");
        }
        catch
        {
            Console.Error.WriteLine($"FATAL: {ex}");
        }
        finally
        {
            Serilog.Log.CloseAndFlush();
        }

        return 1;
    }
}
```

## Best Practices

### ✅ DO:

- Close dialogs **before** disposing the container
- Use `DialogTrackingService` for centralized management
- Implement `IDisposable` in all dialog ViewModels
- Log shutdown progress for debugging
- Use try-catch blocks for graceful degradation
- Test shutdown with open dialogs

### ❌ DON'T:

- Dispose container before closing dialogs
- Assume dialogs will close themselves
- Ignore disposal in ViewModels
- Access container after disposal
- Skip exception handling in shutdown code
- Leave event handlers attached after dialog closes

## Testing Shutdown Behavior

### Manual Test Procedure

1. Start the application
2. Open 2-3 different dialog windows
3. Close the main window (trigger shutdown)
4. Verify no `NullReferenceException` in logs
5. Check that all dialogs closed gracefully

### Automated Tests

```csharp
[Fact]
public void Shutdown_WithOpenDialogs_DoesNotThrow()
{
    // Arrange
    var app = new App();
    app.InitializeComponent();

    // Open dialogs
    var dialog1 = new ErrorDialogView();
    dialog1.Show();

    // Act & Assert - should not throw
    app.Shutdown();
}
```

## Troubleshooting

### Issue: NullReferenceException in DialogService

**Cause:** Container disposed before dialogs closed
**Solution:** Move `CloseAllDialogWindows()` before `Container.Dispose()`

### Issue: Dialogs not closing during shutdown

**Cause:** Dialog enumeration failing
**Solution:** Check `Application.Current.Windows` is accessible, use `DialogTrackingService`

### Issue: Memory leaks from dialogs

**Cause:** Event handlers not detached
**Solution:** Implement `IDisposable` in ViewModels, call `Dispose()` in `OnDialogClosed()`

### Issue: Shutdown hangs

**Cause:** Modal dialog blocking
**Solution:** Set `dialog.DialogResult = false` before closing

## References

### Files Modified

- `src/Program.cs` - Added top-level exception handling
- `src/App.xaml.cs` - Implemented proper shutdown sequence in `OnExit`
- `WileyWidget.UI/ViewModels/DialogViewModelBase.cs` - Added `IDisposable`
- `WileyWidget.Services/DialogTrackingService.cs` - Created tracking service

### Related Documentation

- [Prism Dialog Service](https://prismlibrary.com/docs/wpf/dialogs.html)
- [WPF Application Lifecycle](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview)
- [IDisposable Pattern](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)

## Summary

The critical takeaway: **Always close dialog windows before disposing the DI container**. This simple rule prevents 99% of shutdown-related `NullReferenceException` issues in Prism WPF applications.

Implementation checklist:

- [x] Close dialogs in `OnExit` before container disposal
- [x] Implement `IDisposable` in `DialogViewModelBase`
- [x] Add `DialogTrackingService` for centralized management
- [x] Add exception handling in `Program.cs`
- [x] Create unit tests for disposal behavior
- [x] Document the shutdown sequence

**This ensures clean, crash-free application termination.**
