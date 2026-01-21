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
            // Per Microsoft docs: InvokeRequired returns false if handle doesn't exist
            // AND we're on the creation thread. Must also check IsHandleCreated for safety.
            if (control.InvokeRequired)
            {
                // We're on a different thread - marshal to the UI thread synchronously
                // Invoke() blocks until the delegate completes
                control.Invoke(action);
            }
            else
            {
                // We're already on the UI thread - execute immediately
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
            if (control.InvokeRequired)
            {
                // Synchronously get the task, then await it
                var task = (System.Threading.Tasks.Task)control.Invoke(asyncAction) 
                    ?? throw new InvalidOperationException("Async action returned null task");
                await task;
            }
            else
            {
                // Already on UI thread, execute directly
                await asyncAction();
            }
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
            if (control.InvokeRequired)
            {
                // Synchronously get the task, then await it
                var task = (System.Threading.Tasks.Task<T>)control.Invoke(asyncFunc)
                    ?? throw new InvalidOperationException("Async function returned null task");
                return await task;
            }
            else
            {
                // Already on UI thread
                return await asyncFunc();
            }
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
}
