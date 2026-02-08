using System;
using System.Net.Sockets;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Helper methods for conversational AI interactions.
    /// </summary>
    public static class ConversationalAIHelper
    {
        /// <summary>
        /// Gets a welcome message tailored to the current AI personality.
        /// Returns JARVIS-specific welcome for JARVIS personality, generic otherwise.
        /// </summary>
        /// <param name="personalityName">The name of the current AI personality.</param>
        /// <returns>A welcome message appropriate to the personality.</returns>
        public static string GetWelcomeMessage(string? personalityName)
        {
            // Return JARVIS-specific welcome message if personality is JARVIS-related
            if (!string.IsNullOrEmpty(personalityName) && personalityName.Contains("JARVIS", StringComparison.OrdinalIgnoreCase))
            {
                return "Welcome back, sir. JARVIS online. Your utility empire awaits optimization. What financial chaos shall we conquer today? MORE COWBELL!";
            }

            return $"Hello! I'm your AI assistant with a {personalityName} personality. How can I help you today?";
        }

        /// <summary>
        /// Formats exceptions into user-friendly error messages with appropriate emoji indicators.
        /// </summary>
        /// <param name="exception">The exception to format.</param>
        /// <returns>A friendly error message suitable for display to the user.</returns>
        public static string FormatFriendlyError(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            // Check message content for specific cases
            var message = exception.Message ?? string.Empty;

            return exception switch
            {
                // Socket/network errors
                System.Net.Sockets.SocketException =>
                    "🌐 Network error. Please check your connection and try again.",
                // Task cancellation/timeout
                TaskCanceledException =>
                    "⏱️ Request timed out. Please try again.",
                // HTTP request errors - check specific patterns
                System.Net.Http.HttpRequestException when message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                                         message.Contains("401", StringComparison.OrdinalIgnoreCase) =>
                    "⚠️ AI service is not configured. Please check your API key settings.",
                System.Net.Http.HttpRequestException when message.Contains("deprecated", StringComparison.OrdinalIgnoreCase) ||
                                                         message.Contains("not found", StringComparison.OrdinalIgnoreCase) =>
                    "⚠️ Configuration error. Please check your model configuration settings.",
                // Generic HTTP request exception
                System.Net.Http.HttpRequestException =>
                    "🌐 Network error. Please check your connection and try again.",
                // Invalid operation with API key
                InvalidOperationException when message.Contains("API key", StringComparison.OrdinalIgnoreCase) =>
                    "⚠️ AI service is not configured. Please check your API key settings.",
                // Default
                _ => $"❌ Error: {message}"
            };
        }
    }
}
