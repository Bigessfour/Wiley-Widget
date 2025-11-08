using System;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Extension methods for exception handling and diagnostics.
    /// </summary>
    public static class PrismExceptionExtensions
    {
        /// <summary>
        /// Gets the root exception in the exception chain for better error diagnostics.
        /// Traverses InnerException until the deepest exception is found.
        /// </summary>
        /// <param name="exception">The exception to get the root of</param>
        /// <returns>The root exception</returns>
        public static Exception GetRootException(this Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }

        /// <summary>
        /// Gets a detailed exception message including the full exception chain.
        /// </summary>
        /// <param name="exception">The exception to format</param>
        /// <returns>A detailed exception message</returns>
        public static string GetDetailedMessage(this Exception exception)
        {
            if (exception == null)
            {
                return "Null exception";
            }

            var messages = new System.Collections.Generic.List<string>();
            var current = exception;

            do
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
            }
            while (current != null);

            return string.Join(" -> ", messages);
        }
    }
}
