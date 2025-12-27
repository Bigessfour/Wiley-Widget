using System;

namespace WileyWidget.WinForms.Helpers
{
    /// <summary>
    /// Helper methods for conversational AI interactions.
    /// </summary>
    /// <summary>
    /// Represents a class for conversationalaihelper.
    /// </summary>
    /// <summary>
    /// Represents a class for conversationalaihelper.
    /// </summary>
    /// <summary>
    /// Represents a class for conversationalaihelper.
    /// </summary>
    /// <summary>
    /// Represents a class for conversationalaihelper.
    /// </summary>
    public static class ConversationalAIHelper
    {
        /// <summary>
        /// Performs getwelcomemessage. Parameters: personalityName.
        /// </summary>
        /// <param name="personalityName">The personalityName.</param>
        /// <summary>
        /// Performs getwelcomemessage. Parameters: personalityName.
        /// </summary>
        /// <param name="personalityName">The personalityName.</param>
        /// <summary>
        /// Performs getwelcomemessage. Parameters: personalityName.
        /// </summary>
        /// <param name="personalityName">The personalityName.</param>
        /// <summary>
        /// Performs getwelcomemessage. Parameters: personalityName.
        /// </summary>
        /// <param name="personalityName">The personalityName.</param>
        public static string GetWelcomeMessage(string personalityName)
        {
            return $"Hello! I'm your AI assistant with a {personalityName} personality. How can I help you today?";
        }
        /// <summary>
        /// Performs formatfriendlyerror. Parameters: exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <summary>
        /// Performs formatfriendlyerror. Parameters: exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <summary>
        /// Performs formatfriendlyerror. Parameters: exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <summary>
        /// Performs formatfriendlyerror. Parameters: exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <summary>
        /// Performs formatfriendlyerror. Parameters: exception.
        /// </summary>
        /// <param name="exception">The exception.</param>

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
