#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Implementation of IDispatcherHelper using SynchronizationContext for cross-platform UI thread marshaling.
/// Works with WinForms, WPF, and other UI frameworks that set up a SynchronizationContext.
/// </summary>
public class DispatcherHelper : IDispatcherHelper
{
    private readonly SynchronizationContext _syncContext;
    private readonly int _uiThreadId;
    private readonly ILogger<DispatcherHelper>? _logger;

    public DispatcherHelper()
    {
        // Capture the current SynchronizationContext - must be called from UI thread
        _syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("No SynchronizationContext available. DispatcherHelper must be created on the UI thread.");
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public DispatcherHelper(SynchronizationContext syncContext)
    {
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    public DispatcherHelper(SynchronizationContext syncContext, ILogger<DispatcherHelper> logger)
    {
        _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
        _uiThreadId = Thread.CurrentThread.ManagedThreadId;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current thread is the UI thread
    /// </summary>
    public bool CheckAccess()
    {
        return Thread.CurrentThread.ManagedThreadId == _uiThreadId;
    }

    /// <summary>
    /// Executes an action on the UI thread synchronously
    /// </summary>
    /// <param name="action">The action to execute</param>
    public void Invoke(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;

        if (CheckAccess())
        {
            _logger?.LogTrace("DispatcherHelper.Invoke - Already on UI thread (ThreadId: {ThreadId})", callingThreadId);
            action();
        }
        else
        {
            _logger?.LogTrace("DispatcherHelper.Invoke - Marshalling from ThreadId: {CallingThread} to UI ThreadId: {UIThread}",
                callingThreadId, _uiThreadId);

            Exception? capturedException = null;
            _syncContext.Send(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            }, null);

            if (capturedException != null)
                throw capturedException;
        }
    }

    /// <summary>
    /// Executes a function on the UI thread synchronously and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <returns>The result of the function</returns>
    public T Invoke<T>(Func<T> func)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;

        if (CheckAccess())
        {
            _logger?.LogTrace("DispatcherHelper.Invoke<T> - Already on UI thread (ThreadId: {ThreadId})", callingThreadId);
            return func();
        }
        else
        {
            _logger?.LogTrace("DispatcherHelper.Invoke<T> - Marshalling from ThreadId: {CallingThread} to UI ThreadId: {UIThread}",
                callingThreadId, _uiThreadId);

            T result = default!;
            Exception? capturedException = null;
            _syncContext.Send(_ =>
            {
                try
                {
                    result = func();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            }, null);

            if (capturedException != null)
                throw capturedException;

            return result;
        }
    }

    /// <summary>
    /// Executes an action on the UI thread asynchronously
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>A task representing the async operation</returns>
    public Task InvokeAsync(Action action)
    {
        return InvokeAsync(action, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <returns>A task representing the async operation with result</returns>
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        return InvokeAsync(func, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Executes an action on the UI thread asynchronously with priority
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="priority">The dispatcher priority (informational in WinForms context)</param>
    /// <returns>A task representing the async operation</returns>
    public Task InvokeAsync(Action action, DispatcherPriority priority)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogTrace("DispatcherHelper.InvokeAsync - Priority: {Priority}, ThreadId: {CallingThread} -> UI ThreadId: {UIThread}",
            priority, callingThreadId, _uiThreadId);

        var tcs = new TaskCompletionSource<bool>();
        _syncContext.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously with priority and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="priority">The dispatcher priority (informational in WinForms context)</param>
    /// <returns>A task representing the async operation with result</returns>
    public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogTrace("DispatcherHelper.InvokeAsync<T> - Priority: {Priority}, ThreadId: {CallingThread} -> UI ThreadId: {UIThread}",
            priority, callingThreadId, _uiThreadId);

        var tcs = new TaskCompletionSource<T>();
        _syncContext.Post(_ =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }
}
