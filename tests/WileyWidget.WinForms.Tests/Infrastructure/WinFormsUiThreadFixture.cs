using System;
using System.Threading;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Tests.Infrastructure;

public sealed class WinFormsUiThreadFixture : IDisposable
{
    private readonly Thread _uiThread;
    private readonly TaskCompletionSource<Control> _invokerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ApplicationContext? _context;
    private Control? _invoker;

    public WinFormsUiThreadFixture()
    {
        _uiThread = new Thread(UiThreadStart)
        {
            IsBackground = true,
            Name = "WinFormsUiTestThread"
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        _invoker = _invokerReady.Task.GetAwaiter().GetResult();
    }

    public void Run(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (_invoker is null)
        {
            throw new ObjectDisposedException(nameof(WinFormsUiThreadFixture));
        }

        if (_invoker.InvokeRequired)
        {
            _invoker.Invoke(action);
            return;
        }

        action();
    }

    public T Run<T>(Func<T> func)
    {
        if (func == null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        if (_invoker is null)
        {
            throw new ObjectDisposedException(nameof(WinFormsUiThreadFixture));
        }

        if (_invoker.InvokeRequired)
        {
            return (T)_invoker.Invoke(func);
        }

        return func();
    }

    private void UiThreadStart()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            _context = new ApplicationContext();

            var invoker = new Control();
            invoker.CreateControl();
            _ = invoker.Handle;

            _invokerReady.TrySetResult(invoker);

            Application.Run(_context);

            invoker.Dispose();
        }
        catch (Exception ex)
        {
            _invokerReady.TrySetException(ex);
            throw;
        }
    }

    public void Dispose()
    {
        var invoker = _invoker;
        if (invoker is null)
        {
            return;
        }

        _invoker = null;

        try
        {
            if (!invoker.IsDisposed)
            {
                invoker.Invoke(() => _context?.ExitThread());
            }
        }
        catch
        {
            // Best-effort shutdown; tests should still continue.
        }

        if (_uiThread.IsAlive)
        {
            _uiThread.Join(TimeSpan.FromSeconds(10));
        }

        // Dispose IDisposable fields
        _context?.Dispose();
        _context = null;
        invoker?.Dispose();
    }
}
