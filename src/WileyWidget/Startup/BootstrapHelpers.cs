using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Prism.Ioc;
using Serilog;

namespace WileyWidget.Startup
{
    public static class BootstrapHelpers
    {
        /// <summary>
        /// Runs the Bootstrapper to register all infrastructure services
        /// </summary>
        public static IConfiguration TryRunBootstrapper(IContainerRegistry containerRegistry)
        {
            var bootstrapper = new Bootstrapper();
            return bootstrapper.Run(containerRegistry);
        }

        public static Exception UnwrapTargetInvocationException(Exception? exception)
        {
            if (exception == null)
            {
                return new InvalidOperationException("Exception was null");
            }

            var current = exception;

            // Unwrap TargetInvocationException and AggregateException layers
            while (true)
            {
                if (current is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
                {
                    current = tie.InnerException;
                    continue;
                }

                if (current is AggregateException ae)
                {
                    current = ae.Flatten().InnerException ?? current;
                    continue;
                }

                break;
            }

            return current;
        }

        public static TResult RetryOnException<TResult>(Func<TResult> operation, int maxAttempts = 3, int initialDelayMs = 200)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int attempts = 0;
            int delay = initialDelayMs;

            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    attempts++;
                    Log.Warning(ex, "Attempt {Attempt}/{Max} failed; retrying in {Delay}ms", attempts, maxAttempts, delay);
                    if (attempts >= maxAttempts) throw;

                    // Use Task.Delay().Wait() instead of Thread.Sleep for better async compatibility
                    // This allows the thread to be released back to the thread pool during the delay
                    Task.Delay(delay).Wait();
                    delay = Math.Min(delay * 2, 5000);
                }
            }
        }

        /// <summary>
        /// Async version of RetryOnException with exponential backoff.
        /// Provides better WPF responsiveness by using async/await pattern.
        /// </summary>
        public static async Task<TResult> RetryOnExceptionAsync<TResult>(
            Func<Task<TResult>> operation,
            int maxAttempts = 3,
            int initialDelayMs = 200,
            CancellationToken cancellationToken = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int attempts = 0;
            int delay = initialDelayMs;

            while (true)
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempts < maxAttempts)
                {
                    attempts++;
                    Log.Warning(ex, "Attempt {Attempt}/{Max} failed; retrying in {Delay}ms", attempts, maxAttempts, delay);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = Math.Min(delay * 2, 5000);
                }
            }
        }

        public static void LogExceptionDetails(Exception? ex, bool includeStackTrace = true)
        {
            if (ex == null)
            {
                Log.Debug("LogExceptionDetails called with null exception");
                return;
            }

            int depth = 0;
            Exception? current = ex;
            while (current != null && depth < 10)
            {
                Log.Error(current, "[ExceptionDepth:{Depth}] {ExceptionType}: {Message}", depth, current.GetType().FullName, current.Message);

                if (includeStackTrace && !string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    Log.Error("Stack trace: {StackTrace}", current.StackTrace);
                }

                current = current.InnerException;
                depth++;
            }

            if (depth >= 10)
            {
                Log.Warning("Exception chain exceeded {MaxDepth} levels; truncated", 10);
            }
        }

        private static readonly Regex PlaceholderRegex = new("\\$\\{(?<name>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Extension method to resolve configuration placeholders like ${VARIABLE_NAME}
        /// </summary>
        public static void ResolvePlaceholders(this IConfigurationRoot configurationRoot)
        {
            if (configurationRoot == null)
            {
                return;
            }

            var values = configurationRoot.AsEnumerable().ToList();
            foreach (var entry in values)
            {
                if (string.IsNullOrWhiteSpace(entry.Value) || entry.Value.IndexOf("${", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var replaced = PlaceholderRegex.Replace(entry.Value, match =>
                {
                    var variableName = match.Groups["name"].Value;
                    if (string.IsNullOrWhiteSpace(variableName))
                    {
                        return match.Value;
                    }

                    var resolved = Environment.GetEnvironmentVariable(variableName);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        resolved = configurationRoot[variableName];
                    }

                    return string.IsNullOrEmpty(resolved) ? match.Value : resolved;
                });

                if (!string.Equals(replaced, entry.Value, StringComparison.Ordinal))
                {
                    configurationRoot[entry.Key] = replaced;
                }
            }
        }

        /// <summary>
        /// Legacy method - use ResolvePlaceholders extension method instead
        /// </summary>
        [Obsolete("Use configurationRoot.ResolvePlaceholders() extension method instead")]
        public static void ResolveConfigurationPlaceholders(IConfigurationRoot configurationRoot)
        {
            configurationRoot?.ResolvePlaceholders();
        }
    }
}
