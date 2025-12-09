using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.Tests;

internal static class StaThreadInvoker
{
    public static Task RunAsync(Func<Task> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var tcs = new TaskCompletionSource<object?>();
        StartStaThread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task RunAsync(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var tcs = new TaskCompletionSource<object?>();
        StartStaThread(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var tcs = new TaskCompletionSource<T>();
        StartStaThread(() =>
        {
            try
            {
                var result = action().GetAwaiter().GetResult();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task<T> RunAsync<T>(Func<T> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var tcs = new TaskCompletionSource<T>();
        StartStaThread(() =>
        {
            try
            {
                var result = action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static void StartStaThread(Action action)
    {
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            action();
        })
        {
            IsBackground = true,
            Name = "StaThreadInvoker"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}
