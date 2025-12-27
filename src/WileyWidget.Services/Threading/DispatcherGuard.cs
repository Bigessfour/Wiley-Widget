using System;
using System.Diagnostics;
using WileyWidget.Services.Threading;

namespace WileyWidget.Services.Threading
{
#if DEBUG
    /// <summary>
    /// Represents a class for dispatcherguard.
    /// </summary>
    public static class DispatcherGuard
    {
        /// <summary>
        /// Performs ensureonuithread. Parameters: dispatcher, null.
        /// </summary>
        /// <param name="dispatcher">The dispatcher.</param>
        /// <param name="null">The null.</param>
        public static void EnsureOnUIThread(this IDispatcherHelper dispatcher, string? context = null)
        {
            if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));
            if (!dispatcher.CheckAccess())
            {
                var message = $"UI-thread violation{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" in {context}")}.";
                Debug.Fail(message);
            }
        }
    }
#endif
}
