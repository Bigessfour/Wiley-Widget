#nullable enable

using System;
using System.Threading.Tasks;

namespace WileyWidget.Services.Threading
{
    /// <summary>
    /// Simple interface for UI thread marshaling operations
    /// Since WinUI doesn't require STA threading like WPF, this is much simpler
    /// </summary>
    public interface IDispatcherHelper
    {
        /// <summary>
        /// Checks if the current thread is the UI thread
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// Executes an action on the UI thread asynchronously
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <returns>A task representing the async operation</returns>
        Task InvokeAsync(Action action);

        /// <summary>
        /// Executes a function on the UI thread asynchronously and returns the result
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="func">The function to execute</param>
        /// <returns>A task representing the async operation with result</returns>
        Task<T> InvokeAsync<T>(Func<T> func);
    }
}
