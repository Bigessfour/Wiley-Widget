using System;

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

            return exception switch
            {
                InvalidOperationException when exception.Message.Contains("API key", StringComparison.OrdinalIgnoreCase) =>
                    "⚠️ AI service is not configured. Please check your API key settings.",
                TaskCanceledException =>
                    "⏱️ Request timed out. Please try again.",
                System.Net.Http.HttpRequestException =>
                    "🌐 Network error. Please check your connection and try again.",
                _ => $"❌ Error: {exception.Message}"
            };
        }
    }
}
