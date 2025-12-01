#nullable enable

using System;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading; // Used only for DispatcherPriority enum in the shared interface
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services.Threading
{
    /// <summary>
    /// WinForms-backed implementation of <see cref="IDispatcherHelper"/>.
    /// Uses the captured <see cref="SynchronizationContext"/> for UI-thread marshaling
    /// when available and falls back to sensible defaults when it's not.
    /// </summary>
    public class WinFormsDispatcherHelper : IDispatcherHelper
    {
        private SynchronizationContext? _uiContext;
        private readonly ILogger<WinFormsDispatcherHelper>? _logger;
        private readonly WileyWidget.Services.Abstractions.ITelemetryService? _telemetry;
        private long _marshalCount;

        public WinFormsDispatcherHelper()
        {
            // Capture the current SynchronizationContext. In Program.Main we will make sure
            // this is created on the UI thread before DI resolution so this will point to the
            // WinForms SynchronizationContext (WindowsFormsSynchronizationContext).
            _uiContext = SynchronizationContext.Current;
        }

        public WinFormsDispatcherHelper(ILogger<WinFormsDispatcherHelper> logger) : this()
        {
            _logger = logger;
        }

        public WinFormsDispatcherHelper(ILogger<WinFormsDispatcherHelper> logger, WileyWidget.Services.Abstractions.ITelemetryService telemetry) : this()
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        private void EnsureUiContextCaptured()
        {
            // Capture the current synchronization context if it becomes available on the UI thread
            if (_uiContext == null)
            {
                _uiContext = SynchronizationContext.Current;
                if (_uiContext != null)
                {
                    _logger?.LogTrace("WinFormsDispatcherHelper captured UI SynchronizationContext: {Context}", _uiContext.GetType().FullName);
                }
            }
        }

        public bool CheckAccess()
        {
            EnsureUiContextCaptured();

            // If we have a captured UI context compare the current context reference
            // as a fast check. When contexts are not available fall back to true which
            // means callers should treat the environment as "UI thread" by default.
            return SynchronizationContext.Current == _uiContext || _uiContext == null;
        }

        public void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            EnsureUiContextCaptured();

            if (CheckAccess())
            {
                _logger?.LogTrace("WinFormsDispatcherHelper.Invoke - already on UI thread");
                action();
                return;
            }

            if (_uiContext != null)
            {
                var exceptionHolder = (Exception?)null;
                var done = new ManualResetEventSlim(false);

                _uiContext.Send(_ =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        exceptionHolder = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                }, null);

                done.Wait();

                if (exceptionHolder != null)
                {
                    try { _telemetry?.RecordException(exceptionHolder, ("operation", "Invoke")); } catch { }
                    // rethrow preserving the original stack if possible
                    throw new TargetInvocationException("Exception thrown while invoking action on UI thread", exceptionHolder);
                }

                return;
            }

            // Last-resort synchronous fallback: execute on current thread
            _logger?.LogWarning("WinFormsDispatcherHelper.Invoke - UI context not available, running action on calling thread");
            action();
        }

        public T Invoke<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            if (CheckAccess()) return func();

            if (_uiContext != null)
            {
                T? result = default;
                var exceptionHolder = (Exception?)null;
                var done = new ManualResetEventSlim(false);

                _uiContext.Send(_ =>
                {
                    try
                    {
                        result = func();
                    }
                    catch (Exception ex)
                    {
                        exceptionHolder = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                }, null);

                done.Wait();

                if (exceptionHolder != null)
                {
                    try { _telemetry?.RecordException(exceptionHolder, ("operation", "Invoke<T>")); } catch { }
                    throw new TargetInvocationException("Exception thrown while invoking func on UI thread", exceptionHolder);
                }

                return result!;
            }

            _logger?.LogWarning("WinFormsDispatcherHelper.Invoke<T> - UI context not available, running func on calling thread");
            return func();
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

            EnsureUiContextCaptured();

            if (CheckAccess())
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            if (_uiContext != null)
            {
                var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                try
                {
                    _uiContext.Post(_ =>
                    {
                        try
                        {
                            Interlocked.Increment(ref _marshalCount);
                            action();
                            tcs.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            try { _telemetry?.RecordException(ex, ("operation", "InvokeAsync(Action)")); } catch { }
                            tcs.TrySetException(ex);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }

                return tcs.Task;
            }

            _logger?.LogWarning("WinFormsDispatcherHelper.InvokeAsync - UI context not available, running action on calling thread");
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            EnsureUiContextCaptured();

            if (CheckAccess())
            {
                try
                {
                    return Task.FromResult(func());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            if (_uiContext != null)
            {
                var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    _uiContext.Post(_ =>
                    {
                        try
                        {
                            Interlocked.Increment(ref _marshalCount);
                            tcs.TrySetResult(func());
                        }
                        catch (Exception ex)
                        {
                            try { _telemetry?.RecordException(ex, ("operation", "InvokeAsync<T>")); } catch { }
                            tcs.TrySetException(ex);
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }

                return tcs.Task;
            }

            _logger?.LogWarning("WinFormsDispatcherHelper.InvokeAsync<T> - UI context not available, running func on calling thread");
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }
    }
}
