#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Simplified WinUI dispatcher helper - no STA requirement like WPF
/// </summary>
public class DispatcherHelper : IDispatcherHelper
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogger<DispatcherHelper>? _logger;

    public DispatcherHelper()
    {
        // Get the dispatcher queue - WinUI doesn't require STA threading
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public DispatcherHelper(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    public DispatcherHelper(DispatcherQueue dispatcherQueue, ILogger<DispatcherHelper> logger)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current thread is the UI thread
    /// </summary>
    public bool CheckAccess()
    {
        return _dispatcherQueue.HasThreadAccess;
    }

    /// <summary>
    /// Executes an action on the UI thread asynchronously
    /// Since WinUI doesn't require STA, this is much simpler than WPF
    /// </summary>
    public async Task InvokeAsync(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (CheckAccess())
        {
            // Already on UI thread, execute directly
            _logger?.LogTrace("Already on UI thread, executing directly");
            action();
            return;
        }

        // Need to marshal to UI thread
        _logger?.LogTrace("Marshalling to UI thread");
        var tcs = new TaskCompletionSource<bool>();

        var success = _dispatcherQueue.TryEnqueue(() =>
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
        });

        if (!success)
        {
            throw new InvalidOperationException("Failed to enqueue action on dispatcher queue");
        }

        await tcs.Task;
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously and returns the result
    /// </summary>
    public async Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        if (CheckAccess())
        {
            // Already on UI thread, execute directly
            _logger?.LogTrace("Already on UI thread, executing function directly");
            return func();
        }

        // Need to marshal to UI thread
        _logger?.LogTrace("Marshalling function to UI thread");
        var tcs = new TaskCompletionSource<T>();

        var success = _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!success)
        {
            throw new InvalidOperationException("Failed to enqueue function on dispatcher queue");
        }

        return await tcs.Task;
    }
}