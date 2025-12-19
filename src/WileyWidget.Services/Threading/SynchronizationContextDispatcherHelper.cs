#nullable enable

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading
{
    /// <summary>
    /// Dispatcher helper implementation backed by <see cref="SynchronizationContext"/>.
    /// Designed for WinForms (WindowsFormsSynchronizationContext) and other synchronization contexts.
    /// </summary>
    public sealed class SynchronizationContextDispatcherHelper : IDispatcherHelper
    {
        private readonly SynchronizationContext? _syncContext;
        private readonly ILogger<SynchronizationContextDispatcherHelper>? _logger;

        public SynchronizationContextDispatcherHelper(SynchronizationContext? synchronizationContext, ILogger<SynchronizationContextDispatcherHelper>? logger = null)
        {
            _syncContext = synchronizationContext ?? SynchronizationContext.Current;
            _logger = logger;
        }

        /// <summary>
        /// Returns true when the current thread has the captured synchronization context.
        /// </summary>
        public bool CheckAccess()
        {
            if (_syncContext != null)
            {
                return SynchronizationContext.Current == _syncContext;
            }

            return false;
        }

        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (CheckAccess())
            {
                _logger?.LogTrace("Invoke - Already on UI thread");
                action();
                return;
            }

            if (_syncContext != null)
            {
                // Prefer synchronous Send; if it's not supported, fall back to Post + wait
                try
                {
                    Exception? capturedEx = null;
                    _syncContext.Send(state =>
                    {
                        try
                        {
                            ((Action)state)();
                        }
                        catch (Exception ex)
                        {
                            capturedEx = ex;
                        }
                    }, action);

                    if (capturedEx != null)
                    {
                        ExceptionDispatchInfo.Capture(capturedEx).Throw();
                    }

                    return;
                }
                catch (NotSupportedException notSupEx)
                {
                    _logger?.LogDebug(notSupEx, "SynchronizationContext.Send not supported - falling back to Post+Wait");
                    PostAndWait(action);
                    return;
                }
                catch (Exception sendEx)
                {
                    _logger?.LogDebug(sendEx, "SynchronizationContext.Send threw; attempting Post+Wait fallback");
                    PostAndWait(action);
                    return;
                }
            }

            _logger?.LogWarning("Invoke called but SynchronizationContext is null - executing action synchronously on calling thread");
            action();
        }

        public T Invoke<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            if (CheckAccess())
            {
                _logger?.LogTrace("Invoke<T> - Already on UI thread");
                return func();
            }

            if (_syncContext != null)
            {
                try
                {
                    T? result = default;
                    Exception? capturedEx = null;

                    _syncContext.Send(state =>
                    {
                        try
                        {
                            result = ((Func<T>)state)();
                        }
                        catch (Exception ex)
                        {
                            capturedEx = ex;
                        }
                    }, func);

                    if (capturedEx != null)
                    {
                        ExceptionDispatchInfo.Capture(capturedEx).Throw();
                    }

                    return result!;
                }
                catch (NotSupportedException notSupEx)
                {
                    _logger?.LogDebug(notSupEx, "SynchronizationContext.Send not supported - falling back to Post+Wait<T>");
                    return PostAndWait(func);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "SynchronizationContext.Send threw - attempting Post+Wait<T> fallback");
                    return PostAndWait(func);
                }
            }

            // Fallback: run on threadpool and block
            return Task.Run(func).GetAwaiter().GetResult();
        }

        private void PostAndWait(Action action)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _syncContext!.Post(state =>
                {
                    try
                    {
                        ((Action)state)();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, action);
            }
            catch (Exception postEx)
            {
                // If Post itself fails, fall back to executing synchronously on calling thread and log.
                _logger?.LogWarning(postEx, "SynchronizationContext.Post threw; executing action synchronously on calling thread");
                action();
                return;
            }

            // Block and propagate exceptions, preserving stack trace
            tcs.Task.GetAwaiter().GetResult();
        }

        private T PostAndWait<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _syncContext!.Post(state =>
                {
                    try
                    {
                        var res = ((Func<T>)state)();
                        tcs.SetResult(res);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, func);
            }
            catch (Exception postEx)
            {
                _logger?.LogWarning(postEx, "SynchronizationContext.Post threw; executing func synchronously on calling thread");
                return func();
            }

            return tcs.Task.GetAwaiter().GetResult();
        }

        public Task InvokeAsync(Action action)
        {
            return InvokeAsync(action, DispatcherPriority.Normal);
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return InvokeAsync(func, DispatcherPriority.Normal);
        }

        public Task InvokeAsync(Action action, DispatcherPriority priority)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            if (_syncContext != null)
            {
                var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _syncContext.Post(state =>
                {
                    try
                    {
                        ((Action)state)();
                        tcs.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, action);

                return tcs.Task;
            }

            return Task.Run(action);
        }

        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            if (CheckAccess())
            {
                return Task.FromResult(func());
            }

            if (_syncContext != null)
            {
                var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                _syncContext.Post(state =>
                {
                    try
                    {
                        var res = ((Func<T>)state)();
                        tcs.SetResult(res);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, func);

                return tcs.Task;
            }

            return Task.Run(func);
        }
    }
}
