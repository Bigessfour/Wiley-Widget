#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading;

/// <summary>
/// Implementation of IDispatcherHelper for dispatcher operations
/// </summary>
public class DispatcherHelper : IDispatcherHelper
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<DispatcherHelper>? _logger;

    public DispatcherHelper()
    {
        // Use Application.Current.Dispatcher to ensure we always get the main UI dispatcher
        // This is safer than Dispatcher.CurrentDispatcher which depends on which thread creates the instance
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public DispatcherHelper(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public DispatcherHelper(Dispatcher dispatcher, ILogger<DispatcherHelper> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current thread is the UI thread
    /// </summary>
    public bool CheckAccess()
    {
        return _dispatcher.CheckAccess();
    }

    /// <summary>
    /// Executes an action on the UI thread synchronously
    /// </summary>
    /// <param name="action">The action to execute</param>
    public void Invoke(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;
        var uiThreadId = _dispatcher.Thread.ManagedThreadId;

        if (CheckAccess())
        {
            _logger?.LogTrace("Dispatcher.Invoke - Already on UI thread (ThreadId: {ThreadId})", callingThreadId);
            action();
        }
        else
        {
            _logger?.LogTrace("Dispatcher.Invoke - Marshalling from ThreadId: {CallingThread} to UI ThreadId: {UIThread}",
                callingThreadId, uiThreadId);
            _dispatcher.Invoke(action);
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
        var uiThreadId = _dispatcher.Thread.ManagedThreadId;

        if (CheckAccess())
        {
            _logger?.LogTrace("Dispatcher.Invoke<T> - Already on UI thread (ThreadId: {ThreadId})", callingThreadId);
            return func();
        }
        else
        {
            _logger?.LogTrace("Dispatcher.Invoke<T> - Marshalling from ThreadId: {CallingThread} to UI ThreadId: {UIThread}",
                callingThreadId, uiThreadId);
            return _dispatcher.Invoke(func);
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
    /// <param name="priority">The dispatcher priority</param>
    /// <returns>A task representing the async operation</returns>
    public Task InvokeAsync(Action action, DispatcherPriority priority)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;
        var uiThreadId = _dispatcher.Thread.ManagedThreadId;

        _logger?.LogTrace("Dispatcher.InvokeAsync - Priority: {Priority}, ThreadId: {CallingThread} -> UI ThreadId: {UIThread}",
            priority, callingThreadId, uiThreadId);

        return _dispatcher.InvokeAsync(action, priority).Task;
    }

    /// <summary>
    /// Executes a function on the UI thread asynchronously with priority and returns the result
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="func">The function to execute</param>
    /// <param name="priority">The dispatcher priority</param>
    /// <returns>A task representing the async operation with result</returns>
    public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));

        var callingThreadId = Thread.CurrentThread.ManagedThreadId;
        var uiThreadId = _dispatcher.Thread.ManagedThreadId;

        _logger?.LogTrace("Dispatcher.InvokeAsync<T> - Priority: {Priority}, ThreadId: {CallingThread} -> UI ThreadId: {UIThread}",
            priority, callingThreadId, uiThreadId);

        return _dispatcher.InvokeAsync(func, priority).Task;
    }
}
