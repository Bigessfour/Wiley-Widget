using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace WileyWidget.Startup
{
    public static class BootstrapHelpers
    {
        public static Exception UnwrapTargetInvocationException(Exception? exception)
        {
            if (exception == null)
            {
                return new InvalidOperationException("Exception was null");
            }

            var current = exception;
            var seen = new HashSet<Exception>();

            while (current is System.Reflection.TargetInvocationException tie && tie.InnerException != null && !seen.Contains(tie))
            {
                seen.Add(tie);
                current = tie.InnerException;
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
                catch (Exception)
                {
                    attempts++;
                    if (attempts >= maxAttempts) throw;
                    Thread.Sleep(delay);
                    delay = Math.Min(delay * 2, 5000);
                }
            }
        }

        public static void LogExceptionDetails(Exception? ex)
        {
            if (ex == null)
            {
                Log.Debug("LogExceptionDetails called with null exception");
                return;
            }

            int depth = 0;
            Exception? current = ex;
            while (current != null && depth < 20)
            {
                Log.Error(current, "[ExceptionDepth:{Depth}] {ExceptionType}: {Message}", depth, current.GetType().FullName, current.Message);
                current = current.InnerException;
                depth++;
            }

            if (depth >= 20)
            {
                Log.Warning("Exception chain exceeded {MaxDepth} levels; truncated", 20);
            }
        }

        private static readonly Regex PlaceholderRegex = new("\\$\\{(?<name>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static void ResolveConfigurationPlaceholders(IConfigurationRoot configurationRoot)
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
    }
}
