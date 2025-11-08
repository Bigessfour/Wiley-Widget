# Wiley Widget UI Threading Guidelines

## Overview

This document provides comprehensive guidelines for implementing proper UI threading patterns in the Wiley Widget WPF application. These guidelines are based on Microsoft's WPF threading best practices and the threading infrastructure implemented in this project.

## Core Principles

### 1. Single UI Thread

- **Rule**: All UI elements must be created and accessed on the UI thread
- **Why**: WPF is not thread-safe; accessing UI elements from background threads causes exceptions
- **Exception**: Background threads can queue work to the UI thread via the Dispatcher

### 2. Dispatcher for Thread Marshaling

- **Rule**: Use `Dispatcher.InvokeAsync()` for thread-safe UI updates
- **Why**: Ensures UI updates happen on the correct thread
- **Pattern**: `await DispatcherHelper.InvokeAsync(() => UpdateUI());`

### 3. Task.Run for CPU-bound Work

- **Rule**: Use `Task.Run()` for CPU-intensive operations
- **Why**: Prevents blocking the UI thread
- **Pattern**: `await Task.Run(() => DoCpuWork());`

### 4. Async/Await for I/O Operations

- **Rule**: Use async/await for I/O-bound operations
- **Why**: Provides natural asynchronous programming without blocking
- **Pattern**: `await repository.GetDataAsync();`

## Infrastructure Components

### IDispatcherHelper

Centralized service for UI thread operations:

```csharp
// Synchronous UI update
await DispatcherHelper.InvokeAsync(() => StatusMessage = "Loading...");

// Asynchronous UI update
await DispatcherHelper.InvokeAsync(async () =>
{
    await Task.Delay(100);
    StatusMessage = "Loaded";
});
```

### AsyncViewModelBase

Base class providing threading utilities:

```csharp
public class MyViewModel : AsyncViewModelBase
{
    public MyViewModel(IDispatcherHelper dispatcherHelper, ILogger<MyViewModel> logger)
        : base(dispatcherHelper, logger)
    {
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await ExecuteAsyncOperation(async (ct) =>
        {
            var data = await _repository.GetAllAsync();
            await Enterprises.ReplaceAllAsync(data);
        }, "Load Data");
    }
}
```

### ThreadSafeObservableCollection

Thread-safe collection for UI binding:

```csharp
public ThreadSafeObservableCollection<Enterprise> Enterprises { get; } = new();

// Thread-safe operations
await Enterprises.AddAsync(newItem);
await Enterprises.ReplaceAllAsync(newItems);
await Enterprises.ClearAsync();
```

### IProgressReporter

Progress reporting for long operations:

```csharp
var progress = new ProgressReporter(
    message => StatusMessage = message,
    percentage => ProgressPercentage = percentage);

await ExecuteAsyncOperation(async (ct) =>
{
    progress.ReportStatus("Starting operation...");
    var result = await DoWorkAsync(progress, ct);
    progress.ReportStatus("Operation completed");
}, "My Operation");
```

## Implementation Patterns

### Pattern 1: Data Loading with Progress

```csharp
[RelayCommand]
private async Task LoadEnterprisesAsync()
{
    await ExecuteAsyncOperation(async (ct) =>
    {
        // Database query on background thread
        var enterprises = await _repository.GetAllAsync();

        // UI update on main thread
        await Enterprises.ReplaceAllAsync(enterprises);

        // Additional UI updates
        await DispatcherHelper.InvokeAsync(() =>
        {
            SelectedEnterprise = enterprises.FirstOrDefault();
            UpdateChartData();
        });

    }, "Load Enterprises");
}
```

### Pattern 2: Background Processing with Cancellation

```csharp
[RelayCommand]
private async Task ProcessDataAsync()
{
    await ExecuteAsyncOperation(async (ct) =>
    {
        // CPU-bound work on background thread
        var results = await Task.Run(() =>
            HeavyComputation(_data, ct), ct);

        // Update UI with results
        await DispatcherHelper.InvokeAsync(() =>
        {
            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }
        });

    }, "Process Data");
}
```

### Pattern 3: Sequential Operations

```csharp
[RelayCommand]
private async Task SaveChangesAsync()
{
    await ExecuteAsyncOperation(async (ct) =>
    {
        // Validate on background thread
        await Task.Run(() => ValidateData(), ct);

        // Save to database
        await _repository.SaveAsync(_data);

        // Refresh UI
        await LoadDataAsync();

    }, "Save Changes");
}
```

### Pattern 4: Error Handling

```csharp
[RelayCommand]
private async Task RiskyOperationAsync()
{
    try
    {
        await ExecuteAsyncOperation(async (ct) =>
        {
            // Operation that might fail
            await _service.RiskyCallAsync();
        }, "Risky Operation");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Risky operation failed");
        await DispatcherHelper.InvokeAsync(() =>
        {
            MessageBox.Show($"Operation failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }
}
```

## Anti-Patterns to Avoid

### ❌ Direct UI Access from Background Threads

```csharp
// WRONG - Causes InvalidOperationException
private async Task BadExampleAsync()
{
    await Task.Run(() =>
    {
        // This will crash the application
        StatusMessage = "Loading..."; // UI access from background thread
    });
}
```

### ❌ Blocking the UI Thread

```csharp
// WRONG - Freezes the UI
[RelayCommand]
private void BadSyncExample()
{
    // This blocks the UI thread for 5 seconds
    Thread.Sleep(5000);
    StatusMessage = "Done";
}
```

### ❌ Nested Dispatcher Calls

```csharp
// WRONG - Unnecessary complexity
private async Task BadDispatcherExampleAsync()
{
    await DispatcherHelper.InvokeAsync(async () =>
    {
        await DispatcherHelper.InvokeAsync(() =>
        {
            StatusMessage = "Done";
        });
    });
}
```

### ❌ Ignoring Cancellation

```csharp
// WRONG - Cannot be cancelled
private async Task BadCancellationExampleAsync()
{
    await Task.Run(() =>
    {
        for (int i = 0; i < 1000000; i++)
        {
            // Long operation that ignores cancellation
            DoWork();
        }
    });
}
```

## Best Practices

### 1. Always Use Async/Await

- Prefer async methods over synchronous ones
- Use `await` to maintain responsiveness
- Avoid `.Wait()` and `.Result` which can cause deadlocks

### 2. Proper Error Handling

- Wrap operations in try/catch blocks
- Log errors appropriately
- Show user-friendly error messages on the UI thread

### 3. Cancellation Support

- Always accept `CancellationToken` parameters
- Check `ct.IsCancellationRequested` in loops
- Handle `OperationCanceledException` gracefully

### 4. Progress Reporting

- Use progress reporting for operations > 1 second
- Update progress on the UI thread
- Provide meaningful status messages

### 5. Resource Management

- Dispose of cancellation tokens
- Clean up background resources
- Use `using` statements for disposables

### 6. Testing

- Test threading code thoroughly
- Use synchronization contexts in tests
- Verify UI updates happen on correct thread

## Performance Considerations

### 1. Dispatcher Priority

Choose appropriate dispatcher priority:

```csharp
// High priority for user interactions
await DispatcherHelper.InvokeAsync(() => UpdateUI(),
    DispatcherPriority.Normal);

// Low priority for background updates
await DispatcherHelper.InvokeAsync(() => RefreshCache(),
    DispatcherPriority.Background);
```

### 2. Batching UI Updates

Batch multiple UI updates to reduce dispatcher overhead:

```csharp
await DispatcherHelper.InvokeAsync(() =>
{
    IsLoading = true;
    StatusMessage = "Processing...";
    ProgressPercentage = 0;
    // ... more updates
});
```

### 3. Avoid Unnecessary Marshaling

Check if already on UI thread before marshaling:

```csharp
// DispatcherHelper.InvokeIfRequiredAsync does this automatically
await DispatcherHelper.InvokeAsync(() => UpdateUI());
```

## Migration Guide

### From Old Patterns to New

#### Old: Manual Dispatcher Management

```csharp
// OLD WAY
private async Task OldWayAsync()
{
    var dispatcher = Application.Current.Dispatcher;
    if (dispatcher.CheckAccess())
    {
        IsLoading = true;
    }
    else
    {
        await dispatcher.InvokeAsync(() => IsLoading = true);
    }
}
```

#### New: AsyncViewModelBase

```csharp
// NEW WAY
private async Task NewWayAsync()
{
    await ExecuteAsyncOperation(async (ct) =>
    {
        // Work happens here
        await DoAsyncWork();
    }, "Operation Name");
}
```

## Troubleshooting

### Common Issues

1. **InvalidOperationException**: UI accessed from wrong thread
   - **Solution**: Use `DispatcherHelper.InvokeAsync()`

2. **Deadlock**: Calling `.Wait()` or `.Result` on UI thread
   - **Solution**: Use `await` instead

3. **Race Conditions**: Multiple threads accessing shared state
   - **Solution**: Use proper synchronization or thread-safe collections

4. **UI Not Updating**: Updates not marshaled to UI thread
   - **Solution**: Ensure all UI updates go through dispatcher

### Debugging Tips

- Use `Dispatcher.CheckAccess()` to verify thread
- Enable dispatcher diagnostics in debug builds
- Use logging to track operation flow
- Test on different hardware configurations

## Conclusion

Following these threading guidelines ensures Wiley Widget remains responsive, stable, and maintainable. The provided infrastructure components make proper threading easy to implement and test.

Remember: **UI thread = responsive application**. Keep it free for user interactions, and offload work to background threads appropriately.</content>
<parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\docs\UI_THREADING_GUIDELINES.md
