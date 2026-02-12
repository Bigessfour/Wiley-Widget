using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WileyWidget.WinForms.Services.Abstractions;

namespace WileyWidget.WinForms.Services;

public sealed class WinFormsUiDispatcher : IUiDispatcher, IUiDispatcherInitializer
{
    private Control? _invoker;
    private SynchronizationContext? _uiSynchronizationContext;

    public void Initialize(Control invoker)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        _invoker = invoker;
        _uiSynchronizationContext = SynchronizationContext.Current;
    }

    public bool CheckAccess()
    {
        if (_invoker == null && _uiSynchronizationContext == null && !Application.MessageLoop)
        {
            return true;
        }

        var invoker = _invoker;
        if (invoker is { IsDisposed: false, IsHandleCreated: true })
        {
            try
            {
                return !invoker.InvokeRequired;
            }
            catch (InvalidOperationException)
            {
            }
        }

        var currentContext = SynchronizationContext.Current;
        if (currentContext is not WindowsFormsSynchronizationContext)
        {
            return false;
        }

        return _uiSynchronizationContext == null || ReferenceEquals(currentContext, _uiSynchronizationContext);
    }

    public Task InvokeAsync(Action action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        if (_invoker == null && _uiSynchronizationContext == null && !Application.MessageLoop)
        {
            action();
            return Task.CompletedTask;
        }

        var invoker = _invoker ?? throw new InvalidOperationException("UI dispatcher is not initialized.");
        if (invoker.IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (!invoker.IsHandleCreated)
        {
            throw new InvalidOperationException("UI dispatcher invoker handle is not created.");
        }

        return invoker.InvokeAsync(action, ct);
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(ct);
        }

        if (CheckAccess())
        {
            return Task.FromResult(action());
        }

        if (_invoker == null && _uiSynchronizationContext == null && !Application.MessageLoop)
        {
            return Task.FromResult(action());
        }

        var invoker = _invoker ?? throw new InvalidOperationException("UI dispatcher is not initialized.");
        if (invoker.IsDisposed)
        {
            return Task.FromResult(default(T)!);
        }

        if (!invoker.IsHandleCreated)
        {
            throw new InvalidOperationException("UI dispatcher invoker handle is not created.");
        }

        return invoker.InvokeAsync(action, ct);
    }

    public Task InvokeAsync(Func<CancellationToken, ValueTask> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        if (CheckAccess())
        {
            return action(ct).AsTask();
        }

        if (_invoker == null && _uiSynchronizationContext == null && !Application.MessageLoop)
        {
            return action(ct).AsTask();
        }

        var invoker = _invoker ?? throw new InvalidOperationException("UI dispatcher is not initialized.");
        if (invoker.IsDisposed)
        {
            return Task.CompletedTask;
        }

        if (!invoker.IsHandleCreated)
        {
            throw new InvalidOperationException("UI dispatcher invoker handle is not created.");
        }

        return invoker.InvokeAsync(action, ct);
    }

    public Task<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(ct);
        }

        if (CheckAccess())
        {
            return action(ct).AsTask();
        }

        if (_invoker == null && _uiSynchronizationContext == null && !Application.MessageLoop)
        {
            return action(ct).AsTask();
        }

        var invoker = _invoker ?? throw new InvalidOperationException("UI dispatcher is not initialized.");
        if (invoker.IsDisposed)
        {
            return Task.FromResult(default(T)!);
        }

        if (!invoker.IsHandleCreated)
        {
            throw new InvalidOperationException("UI dispatcher invoker handle is not created.");
        }

        return invoker.InvokeAsync(action, ct);
    }
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public Task InvokeAsync(Action action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(ct);
        }

        return Task.FromResult(action());
    }

    public Task InvokeAsync(Func<CancellationToken, ValueTask> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled(ct);
        }

        return action(ct).AsTask();
    }

    public Task<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(ct);
        }

        return action(ct).AsTask();
    }
}
