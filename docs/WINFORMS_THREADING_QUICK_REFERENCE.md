# WinForms Threading Quick Reference Guide

## 🚀 Quick Decision Tree

```
Are you updating UI (controls, ViewModels, ObservableCollection)?
├─ YES → Use Pattern 1 or Pattern 3
│  ├─ In async method? → Pattern 1: await (default ConfigureAwait)
│  └─ In sync callback? → Pattern 3: InvokeRequired check
│
└─ NO → Are you doing CPU-intensive work?
   ├─ YES → Pattern 2: Task.Run + ConfigureAwait(false)
   └─ NO → Pattern 1: await (default ConfigureAwait)
```

## ✅ Pattern 1: Async/Await for UI Updates (MOST COMMON)

**When:** ViewModel async methods that update UI-bound properties

```csharp
public async Task LoadDataAsync()
{
    // await without ConfigureAwait(false) - captures UI context
    var data = await _repository.GetDataAsync();

    // This runs on UI thread automatically ✅
    BudgetAnalysis = data;
    FundSummaries.Clear();
    foreach (var item in data.Items)
    {
        FundSummaries.Add(item);
    }
}
```

**Key Points:**

- ✅ Use default `await` (no ConfigureAwait)
- ✅ Direct property assignments work
- ✅ ObservableCollection operations safe
- ❌ Don't use ConfigureAwait(false)
- ❌ Don't use SynchronizationContext.Post()

## ✅ Pattern 2: Task.Run for CPU Work

**When:** Heavy CPU calculations that would block UI

```csharp
public async Task ProcessDataAsync()
{
    // Offload to thread pool
    var result = await Task.Run(() =>
    {
        // CPU-intensive work here
        return ExpensiveCalculation();
    });

    // Back on UI thread after await ✅
    ResultLabel.Text = result;
}
```

**Key Points:**

- ✅ Use for CPU-intensive work only
- ✅ Continuation returns to UI thread automatically
- ⚠️ Can use ConfigureAwait(false) **inside** Task.Run if no UI updates follow

## ✅ Pattern 3: InvokeRequired for Direct Control Access

**When:** Methods called from unknown threads (callbacks, events)

```csharp
public void UpdateFromAnyThread(string text)
{
    if (InvokeRequired)
    {
        Invoke(() => UpdateFromAnyThread(text));
        return;
    }

    // Now on UI thread ✅
    textBox1.Text = text;
}
```

**Key Points:**

- ✅ Always check InvokeRequired first
- ✅ Use Invoke (sync) or BeginInvoke (async)
- ✅ Recursive call pattern for simplicity

## ❌ Anti-Patterns to AVOID

### ❌ ConfigureAwait(false) Before UI Update

```csharp
// WRONG - May cause threading exceptions
public async Task LoadDataAsync()
{
    var data = await _repository.GetDataAsync().ConfigureAwait(false);
    BudgetAnalysis = data; // ❌ May run on thread pool!
}
```

### ❌ Manual SynchronizationContext.Post

```csharp
// WRONG - Unnecessary with async/await
_uiContext.Post(_ =>
{
    BudgetAnalysis = data; // ❌ Use await instead
}, null);
```

### ❌ Direct Control Access from Thread Pool

```csharp
// WRONG - Violates WinForms threading model
Task.Run(() =>
{
    textBox1.Text = "Updated"; // ❌ Use InvokeRequired
});
```

## 🔍 When to Use ConfigureAwait(false)

**ONLY use ConfigureAwait(false) when ALL of these are true:**

1. ✅ You're in a library/service (not ViewModel)
2. ✅ No UI updates happen after the await
3. ✅ Performance is critical (high-frequency calls)

**Example - Appropriate Use:**

```csharp
// Inside a repository or service class
public async Task<Data> GetDataAsync()
{
    // No UI context needed after this await
    var result = await _httpClient.GetAsync(url).ConfigureAwait(false);
    return await result.Content.ReadAsAsync<Data>().ConfigureAwait(false);
}
```

**Example - Services with ConfigureAwait(false):**

```csharp
_ = Task.Run(async () =>
{
    // Already on thread pool, no UI updates
    await RunHealthCheckAsync().ConfigureAwait(false);
    await SeedDataAsync().ConfigureAwait(false);
});
```

## 🎯 ViewModel Method Template

```csharp
public async Task LoadSomethingAsync()
{
    try
    {
        IsLoading = true;  // UI property update
        ErrorMessage = null;

        // Repository call - no ConfigureAwait
        var data = await _repository.GetAsync();

        // UI updates - automatic UI thread
        Items.Clear();
        foreach (var item in data)
        {
            Items.Add(item);
        }

        StatusMessage = "Loaded successfully";
    }
    catch (Exception ex)
    {
        ErrorMessage = ex.Message;
        _logger.LogError(ex, "Failed to load data");
    }
    finally
    {
        IsLoading = false;
    }
}
```

## 🔒 Thread-Safe Control Members

**Only these Control members are thread-safe:**

- `Invoke` - Sync marshal to UI thread
- `BeginInvoke` - Async marshal to UI thread
- `EndInvoke` - Complete async invoke
- `InvokeRequired` - Check if marshaling needed
- `CreateGraphics` - Thread-safe graphics

**All other members require UI thread or Invoke!**

## 📋 Code Review Checklist

### ViewModels

- [ ] No ConfigureAwait(false) before UI updates
- [ ] No manual SynchronizationContext usage
- [ ] ObservableCollection updates in async methods
- [ ] INotifyPropertyChanged properties set directly

### Forms/Controls

- [ ] InvokeRequired checks for callbacks
- [ ] No direct control access from Task.Run
- [ ] BeginInvoke for non-blocking updates
- [ ] Invoke for sync updates (use sparingly)

### Services/Repositories

- [ ] ConfigureAwait(false) for non-UI async
- [ ] No Form/Control references
- [ ] Return data, not update UI

## 📚 Microsoft Documentation Links

- [Control.InvokeRequired](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.invokerequired)
- [Control.Invoke](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.invoke)
- [STAThreadAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.stathreadattribute)
- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)

---

**Last Updated:** 2025-01-02
**See Also:** [WINFORMS_THREAD_SAFETY_VALIDATION.md](WINFORMS_THREAD_SAFETY_VALIDATION.md)
