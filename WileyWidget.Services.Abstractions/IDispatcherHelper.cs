#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WileyWidget.Services.Threading
{
    /// <summary>
    /// Interface for dispatcher helper operations to marshal calls to the UI thread
    /// </summary>
    public interface IDispatcherHelper
    {
        /// <summary>
        /// Checks if the current thread is the UI thread
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// Executes an action on the UI thread synchronously
        /// </summary>
        /// <param name="action">The action to execute</param>
        void Invoke(Action action);

        /// <summary>
        /// Executes a function on the UI thread synchronously and returns the result
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="func">The function to execute</param>
        /// <returns>The result of the function</returns>
        T Invoke<T>(Func<T> func);

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

        /// <summary>
        /// Executes an action on the UI thread asynchronously with priority
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="priority">The dispatcher priority</param>
        /// <returns>A task representing the async operation</returns>
        Task InvokeAsync(Action action, DispatcherPriority priority);

        /// <summary>
        /// Executes a function on the UI thread asynchronously with priority and returns the result
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="func">The function to execute</param>
        /// <param name="priority">The dispatcher priority</param>
        /// <returns>A task representing the async operation with result</returns>
        Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority);
    }
}
