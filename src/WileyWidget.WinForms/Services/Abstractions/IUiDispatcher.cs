using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Services.Abstractions;

public interface IUiDispatcher
{
    bool CheckAccess();
    Task InvokeAsync(Action action, CancellationToken ct = default);
    Task<T> InvokeAsync<T>(Func<T> action, CancellationToken ct = default);
    Task InvokeAsync(Func<CancellationToken, ValueTask> action, CancellationToken ct = default);
    Task<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken ct = default);
}

public interface IUiDispatcherInitializer
{
    void Initialize(Control invoker);
}
