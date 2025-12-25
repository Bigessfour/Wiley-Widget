#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services.Threading;

namespace WileyWidget.WinForms.Threading
{
    /// <summary>
    /// Lightweight UI thread helper used by ViewModels.
    /// Delegates to <see cref="IDispatcherHelper"/> resolved from <see cref="Program.Services"/> when available.
    /// Falls back to Task.Run / synchronous calls when no dispatcher is available (e.g., non-UI test runs).
    /// </summary>
    public static class UiThread
    {
        private static IDispatcherHelper? Dispatcher =>
            Program.Services != null
                ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IDispatcherHelper>(Program.Services)
                : null;

        public static bool CheckAccess() => Dispatcher?.CheckAccess() ?? true;

        public static void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Dispatcher != null) Dispatcher.Invoke(action);
            else action();
        }

        public static Task InvokeAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Dispatcher != null ? Dispatcher.InvokeAsync(action) : Task.Run(action);
        }

        public static Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            return Dispatcher != null ? Dispatcher.InvokeAsync(func) : Task.Run(func);
        }

        public static Task InvokeAsync(Action action, DispatcherPriority priority)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return Dispatcher != null ? Dispatcher.InvokeAsync(action, priority) : Task.Run(action);
        }

        public static Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            return Dispatcher != null ? Dispatcher.InvokeAsync(func, priority) : Task.Run(func);
        }
    }
}