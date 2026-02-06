#nullable enable

using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Utils;

/// <summary>
/// Thread-safe helper for marshalling calls to the UI thread per Microsoft WinForms threading best practices.
/// https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls
/// 
/// Windows Forms uses the Single-Threaded Apartment (STA) model - all control access MUST occur on the
/// thread that created the control. This helper ensures safe cross-thread operations.
/// 
/// .NET 10+: Uses Control.InvokeAsync() for non-blocking async marshalling where available.
/// Fallback: Uses synchronous Invoke() for compatibility.
/// </summary>
public static class UIThreadHelper
{
    private const int MessagePumpSleepDelayMs = 1;
    private static readonly TimeSpan MessagePumpTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Safely executes an action on the UI thread if needed.
    /// Uses InvokeRequired to determine if marshalling is necessary.
    /// </summary>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="action">The action to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown if control or action is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the control handle doesn't exist and can't be created.</exception>
    public static void ExecuteOnUIThread(
        this Control control,
        Action action,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (action == null) throw new ArgumentNullException(nameof(action));

        try
        {
            // Guard access to InvokeRequired which may touch the control handle
            // and throw if the control isn't ready on this thread. If reading
            // InvokeRequired fails, treat as needing marshalling and attempt to
            // invoke on the UI thread.
            bool invokeRequired;
            try
            {
                invokeRequired = control.InvokeRequired;
            }
            catch (InvalidOperationException)
            {
                // Could not read InvokeRequired (handle not created or accessed
                // from wrong thread). Assume we need to marshal to the UI thread.
                invokeRequired = true;
            }

            if (invokeRequired)
            {
                // Marshal to the UI thread synchronously
                control.Invoke(action);
            }
            else
            {
                // Already on the UI thread - execute immediately
                action();
            }
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed, cannot execute action on UI thread", control.Name);
            // Don't re-throw - gracefully degrade if control is already disposed
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing action on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an action on the UI thread if needed, with return value.
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="func">The function to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>The result of the function.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or func is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the control handle doesn't exist and can't be created.</exception>
    public static T ExecuteOnUIThread<T>(
        this Control control,
        Func<T> func,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (func == null) throw new ArgumentNullException(nameof(func));

        try
        {
            if (control.InvokeRequired)
            {
                // Marshal synchronously to UI thread
                return (T)control.Invoke(func) ?? throw new InvalidOperationException("Function returned null");
            }
            else
            {
                // Already on UI thread
                return func();
            }
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed, cannot execute function on UI thread", control.Name);
            throw;
        }
        catch (InvalidOperationException ex) when (!ex.Message.Contains("null"))
        {
            logger?.LogError(ex, "Invalid operation executing function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an async operation via the UI thread marshalling pattern.
    /// Per Microsoft docs, async/await is preferred over old BackgroundWorker pattern.
    /// This method safely bridges async code with WinForms UI thread requirements.
    /// </summary>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="asyncAction">The async action to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A task that completes when the action completes on the UI thread.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or asyncAction is null.</exception>
    public static async System.Threading.Tasks.Task ExecuteOnUIThreadAsync(
        this Control control,
        Func<System.Threading.Tasks.Task> asyncAction,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

        try
        {
            System.Threading.Tasks.Task? task;

            if (control.InvokeRequired)
            {
                task = control.Invoke(asyncAction) as System.Threading.Tasks.Task
                    ?? throw new InvalidOperationException("Async action returned null task");
            }
            else
            {
                task = asyncAction()
                    ?? throw new InvalidOperationException("Async action returned null task");
            }

            await AwaitWithOptionalMessagePumpAsync(task, logger);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed during async execution", control.Name);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing async action on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an async operation with return value via the UI thread marshalling pattern.
    /// </summary>
    /// <typeparam name="T">The return type of the async operation.</typeparam>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="asyncFunc">The async function to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>The result of the async function.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or asyncFunc is null.</exception>
    public static async System.Threading.Tasks.Task<T> ExecuteOnUIThreadAsync<T>(
        this Control control,
        Func<System.Threading.Tasks.Task<T>> asyncFunc,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        try
        {
            System.Threading.Tasks.Task<T>? task;

            if (control.InvokeRequired)
            {
                task = control.Invoke(asyncFunc) as System.Threading.Tasks.Task<T>
                    ?? throw new InvalidOperationException("Async function returned null task");
            }
            else
            {
                task = asyncFunc()
                    ?? throw new InvalidOperationException("Async function returned null task");
            }

            return await AwaitWithOptionalMessagePumpAsync(task, logger);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed during async execution", control.Name);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing async function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Checks if a control is ready for UI operations (exists and is accessible).
    /// Per Microsoft docs: must check both InvokeRequired AND IsHandleCreated.
    /// </summary>
    /// <param name="control">The control to check.</param>
    /// <returns>True if the control is ready for safe UI operations.</returns>
    public static bool IsReadyForUIOperations(this Control control)
    {
        if (control == null) return false;
        if (control.IsDisposed) return false;

        // Per Microsoft: InvokeRequired can return false even if handle isn't created
        // (if we're on the creation thread but before OnHandleCreated)
        // So we must explicitly check IsHandleCreated for safety
        return control.IsHandleCreated && !control.Disposing;
    }

    /// <summary>
    /// Simple non-blocking helper that mirrors the common pattern used in many WinForms apps:
    /// if (InvokeRequired) BeginInvoke(action); else action();
    /// This is intentionally fire-and-forget (BeginInvoke) for callers that prefer not to block.
    /// </summary>
    /// <param name="control">Owner control for UI thread.</param>
    /// <param name="action">Action to execute on UI thread.</param>
    /// <param name="logger">Optional logger to record disposal/invalid operation warnings.</param>
    public static void InvokeIfRequired(this Control control, Action action, ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (action == null) throw new ArgumentNullException(nameof(action));

        try
        {
            // Guard access to InvokeRequired similarly to the synchronous path.
            bool invokeRequired;
            try
            {
                invokeRequired = control.InvokeRequired;
            }
            catch (InvalidOperationException)
            {
                // If we can't read InvokeRequired, assume we need to marshal.
                invokeRequired = true;
            }

            if (invokeRequired)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (ObjectDisposedException odex)
                {
                    logger?.LogWarning(odex, "Control {ControlName} disposed while attempting BeginInvoke", control.Name);
                }
                return;
            }

            // Already on UI thread - execute inline
            action();
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed, cannot execute action", control.Name);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing action on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an action on the UI thread asynchronously without blocking.
    /// Uses Control.InvokeAsync() (.NET 10+) for non-blocking marshalling.
    /// Per Microsoft docs: InvokeAsync is the modern async marshalling pattern for WinForms.
    /// https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls
    /// </summary>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="action">The action to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A ValueTask that completes when the action finishes on the UI thread.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or action is null.</exception>
    public static async System.Threading.Tasks.ValueTask InvokeAsyncNonBlocking(
        this Control control,
        Action action,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (action == null) throw new ArgumentNullException(nameof(action));

        try
        {
            // Use Control.InvokeAsync (.NET 10+) for non-blocking marshalling
            // CancellationToken.None means no cancellation support for this operation
            await control.InvokeAsync(action, System.Threading.CancellationToken.None);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed, cannot execute action on UI thread", control.Name);
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogInformation(ex, "Async action cancelled on UI thread for {ControlName}", control.Name);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing action on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes a function on the UI thread asynchronously without blocking.
    /// Uses Control.InvokeAsync() (.NET 10+) for non-blocking marshalling.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="func">The function to execute on the UI thread.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A ValueTask that yields the result when the function completes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or func is null.</exception>
    public static async System.Threading.Tasks.ValueTask<T> InvokeAsyncNonBlocking<T>(
        this Control control,
        Func<T> func,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (func == null) throw new ArgumentNullException(nameof(func));

        try
        {
            // Control.InvokeAsync returns ValueTask<T> directly
            return await control.InvokeAsync(func, System.Threading.CancellationToken.None);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed, cannot execute function on UI thread", control.Name);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogInformation(ex, "Async function cancelled on UI thread for {ControlName}", control.Name);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an async function on the UI thread asynchronously without blocking.
    /// Uses Control.InvokeAsync() (.NET 10+) for non-blocking marshalling.
    /// Bridges async/await patterns with WinForms STA threading model.
    /// </summary>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="asyncFunc">The async function to execute on the UI thread (Func&lt;CancellationToken, ValueTask&gt;).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A ValueTask that completes when the async operation finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or asyncFunc is null.</exception>
    public static async System.Threading.Tasks.ValueTask InvokeAsyncNonBlocking(
        this Control control,
        Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> asyncFunc,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        try
        {
            // InvokeAsync handles async delegates with CancellationToken parameter
            await control.InvokeAsync(asyncFunc);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed during async execution", control.Name);
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogInformation(ex, "Async function cancelled on UI thread for {ControlName}", control.Name);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing async function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an async function with return value on the UI thread asynchronously without blocking.
    /// Uses Control.InvokeAsync() (.NET 10+) for non-blocking marshalling.
    /// </summary>
    /// <typeparam name="T">The return type of the async function.</typeparam>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="asyncFunc">The async function to execute on the UI thread (Func&lt;CancellationToken, ValueTask&lt;T&gt;&gt;).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A ValueTask that yields the result when the async operation completes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or asyncFunc is null.</exception>
    public static async System.Threading.Tasks.ValueTask<T> InvokeAsyncNonBlocking<T>(
        this Control control,
        Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<T>> asyncFunc,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        try
        {
            // InvokeAsync handles async delegates with CancellationToken parameter
            return await control.InvokeAsync(asyncFunc);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed during async execution", control.Name);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogInformation(ex, "Async function cancelled on UI thread for {ControlName}", control.Name);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing async function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Safely executes an async function returning Task on the UI thread asynchronously without blocking.
    /// Overload for Func&lt;CancellationToken, Task&gt; (async delegates that return Task instead of ValueTask).
    /// Uses Control.InvokeAsync() (.NET 10+) for non-blocking marshalling.
    /// </summary>
    /// <param name="control">The control that owns the UI thread.</param>
    /// <param name="asyncFunc">The async function to execute on the UI thread (Func&lt;CancellationToken, Task&gt;).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A Task that completes when the async operation finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if control or asyncFunc is null.</exception>
    public static async System.Threading.Tasks.Task InvokeAsyncNonBlockingTask(
        this Control control,
        Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> asyncFunc,
        ILogger? logger = null)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (asyncFunc == null) throw new ArgumentNullException(nameof(asyncFunc));

        try
        {
            // Convert Func<CancellationToken, Task> to Func<CancellationToken, ValueTask>
            // by wrapping the Task result in a ValueTask
            await control.InvokeAsync(
                async (ct) =>
                {
                    var task = asyncFunc(ct);
                    if (task != null)
                        await task;
                },
                System.Threading.CancellationToken.None);
        }
        catch (ObjectDisposedException ex)
        {
            logger?.LogWarning(ex, "Control {ControlName} is disposed during async execution", control.Name);
        }
        catch (OperationCanceledException ex)
        {
            logger?.LogInformation(ex, "Async function cancelled on UI thread for {ControlName}", control.Name);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation executing async function on UI thread for {ControlName}", control.Name);
            throw;
        }
    }

    /// <summary>
    /// Waits for a control's handle to be created, with timeout protection.
    /// Useful for deferred operations that require a window handle.
    /// </summary>
    /// <param name="control">The control whose handle to wait for.</param>
    /// <param name="timeoutMs">Maximum wait time in milliseconds (default 5000ms).</param>
    /// <returns>True if handle was created within timeout, false if timeout expired.</returns>
    public static bool WaitForHandle(this Control control, int timeoutMs = 5000)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));

        if (control.IsHandleCreated) return true;

        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < endTime)
        {
            // Force handle creation by accessing the Handle property
            // This is safe per Microsoft docs
            try
            {
                _ = control.Handle;
                if (control.IsHandleCreated) return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            // Pump UI messages instead of blocking with Thread.Sleep
            // This keeps the UI responsive and allows pending messages to be processed
            System.Windows.Forms.Application.DoEvents();
        }

        return false;
    }

    private static async System.Threading.Tasks.Task AwaitWithOptionalMessagePumpAsync(
        System.Threading.Tasks.Task task,
        ILogger? logger)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        if (RequiresLocalMessagePump())
        {
            logger?.LogTrace("UIThreadHelper engaged a temporary message pump for async Task execution.");
            PumpMessagesUntilCompleted(task);
        }

        await task.ConfigureAwait(true);
    }

    private static async System.Threading.Tasks.Task<T> AwaitWithOptionalMessagePumpAsync<T>(
        System.Threading.Tasks.Task<T> task,
        ILogger? logger)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        if (RequiresLocalMessagePump())
        {
            logger?.LogTrace("UIThreadHelper engaged a temporary message pump for async Task<T> execution.");
            PumpMessagesUntilCompleted(task);
        }

        return await task.ConfigureAwait(true);
    }

    private static bool RequiresLocalMessagePump()
    {
        if (System.Windows.Forms.Application.MessageLoop)
        {
            return false;
        }

        if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
        {
            return false;
        }

        return System.Threading.SynchronizationContext.Current is System.Windows.Forms.WindowsFormsSynchronizationContext;
    }

    private static void PumpMessagesUntilCompleted(System.Threading.Tasks.Task task)
    {
        var expiration = DateTime.UtcNow + MessagePumpTimeout;

        while (!task.IsCompleted)
        {
            System.Windows.Forms.Application.DoEvents();

            if (task.IsCompleted)
            {
                break;
            }

            System.Threading.Thread.Sleep(MessagePumpSleepDelayMs);

            if (DateTime.UtcNow > expiration)
            {
                throw new TimeoutException("UIThreadHelper timed out while waiting for async UI work to complete.");
            }
        }
    }
}
