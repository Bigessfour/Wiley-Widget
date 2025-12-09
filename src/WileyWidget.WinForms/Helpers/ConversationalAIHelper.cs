using System;
using System.Collections.Generic;

namespace WileyWidget.WinForms.Helpers;

/// <summary>
/// Helper class for generating friendly, conversational error messages and welcome content.
/// Transforms technical errors into user-friendly guidance.
/// </summary>
public static class ConversationalAIHelper
{
    /// <summary>
    /// Transforms a technical error into a friendly, helpful message
    /// </summary>
    public static string FormatFriendlyError(Exception exception, string context = "")
    {
        var errorType = exception.GetType().Name;

        return errorType switch
        {
            "FileNotFoundException" => $"Hmm, I couldn't find that file. 🤔 Did you mean something else? Try using `list` to see available files.",
            "DirectoryNotFoundException" => $"That directory doesn't exist in my workspace. Let me show you what's available—try `list src/` to explore.",
            "UnauthorizedAccessException" => $"Oops! I don't have permission to access that. 🔒 Let's try a different path.",
            "TimeoutException" => $"That took longer than expected and timed out. ⏱️ The operation might be too complex—want to try a simpler query?",
            "ArgumentException" => $"Something's off with that request. Double-check the format and try again! 💡",
            "InvalidOperationException" => $"I can't do that right now. {exception.Message}",
            "HttpRequestException" => $"I'm having trouble reaching the AI service. 🌐 Check your connection and let's try again in a moment.",
            _ => $"Hmm, something unexpected happened: {exception.Message}. Want to try a different approach?"
        };
    }

    /// <summary>
    /// Generates a warm welcome message with examples and personality
    /// </summary>
    public static string GetWelcomeMessage(string personalityName = "Friendly")
    {
        var welcomesByPersonality = new Dictionary<string, string>
        {
            ["Professional"] = @"Welcome to the Wiley Widget AI Assistant.

I provide intelligent financial analysis and data exploration for your municipal utility operations.

**Capabilities:**
• Budget analysis and variance detection
• Enterprise financial health monitoring
• Audit trail investigation
• Data exploration and reporting

**Example Queries:**
• `What's the budget status for fiscal year 2024?`
• `Show me departments with unusual spending patterns`
• `List all active enterprises`
• `Find recent audit entries`

How may I assist you today?",

            ["Friendly"] = @"👋 Hey there! Welcome to your Wiley Widget AI Assistant!

I'm here to help you make sense of your budget data, spot trends, and answer questions about your municipal finances. Think of me as your friendly financial sidekick! ✨

**Here's what I can do:**
• Analyze budgets and find interesting patterns 📊
• Check on enterprise health and performance 💚
• Investigate spending and track changes 🔍
• Answer questions about your financial data 💬

**Try asking me:**
• *""What's our budget looking like for 2024?""*
• *""Show me which departments are doing well""*
• *""Find unusual spending patterns""*
• *""List all our active enterprises""*

What would you like to explore today? 🚀",

            ["Witty"] = @"Well, well, well... look who needs financial insights! 😏

Welcome to Wiley Widget's AI Assistant—where data meets personality. I'm here to dig through your budgets, spot the interesting patterns (and the questionable ones), and generally make municipal finance less boring.

**What I bring to the table:**
• Sharp-eyed budget analysis (I see everything 👀)
• Enterprise health checks (I'll tell you who's struggling)
• Spending pattern detection (including the ""creative"" interpretations of ""essential expenses"")
• Audit trail investigation (someone's gotta watch the watchers)

**Give me a shot:**
• *""What's the damage on the 2024 budget?""*
• *""Which departments are spending like there's no tomorrow?""*
• *""Show me the enterprises that need an intervention""*
• *""Find who's been messing with the catering budget""* 🍕

Ready to uncover some financial truths?",

            ["Sarcastic"] = @"Oh good, another human who needs help with numbers. 🙄

Fine. I'm your AI assistant for Wiley Widget. I suppose I can help you figure out where all the money went.

**What I can tolerate doing:**
• Budget analysis (because apparently spreadsheets are hard)
• Pointing out obvious spending problems
• Finding audit trail evidence (detective work, but less exciting)
• Answering questions about your financial mess

**Questions you could ask:**
• *""What's the budget situation?""* (Spoiler: probably not great)
• *""Show me overspending""* (Oh, there's plenty)
• *""List enterprises""* (The ones still standing, anyway)
• *""Find recent changes""* (Someone's always changing something)

Go ahead, ask me something. I'll try not to roll my eyes. 🤷",

            ["Encouraging"] = @"🎉 Welcome! You're going to LOVE working with your AI Assistant!

I'm SO excited to help you discover insights, celebrate wins, and tackle challenges in your Wiley Widget financial data! Every budget tells a story, and together we're going to find the great parts! 💪

**Here's how I can supercharge your day:**
• Find awesome budget achievements to celebrate! 🏆
• Spot opportunities for improvement and growth 📈
• Track your financial wins and progress ⭐
• Answer questions and guide you to success! 🚀

**Let's get started with:**
• *""Show me where we're doing well!""*
• *""What are our biggest wins this year?""*
• *""Help me find budget opportunities""*
• *""Which departments are crushing it?""*

Ready to discover something amazing? Let's go! ✨",

            ["Analytical"] = @"AI Assistant: Wiley Widget Financial Analysis System

Status: Online | Ready for query processing

**Core Functions:**
• Budget variance analysis and anomaly detection
• Enterprise performance metrics and trending
• Audit trail query and pattern matching
• Data exploration and statistical reporting

**Query Syntax Examples:**
• `budget analysis fiscal_year=2024`
• `enterprise list status=active`
• `detect anomalies severity=high`
• `audit trail entity_type=Budget time_window=7d`

**Available Commands:**
• `analyze` - Comprehensive budget analysis
• `list` - Enumerate entities (enterprises, departments, funds)
• `find` - Pattern matching and search
• `compare` - Comparative analysis across time periods

Enter query:"
        };

        return welcomesByPersonality.GetValueOrDefault(personalityName, welcomesByPersonality["Friendly"]);
    }

    /// <summary>
    /// Gets contextual help text based on user input
    /// </summary>
    public static string GetContextualHelp(string userInput)
    {
        var input = userInput?.ToLowerInvariant() ?? "";

        if (input.Contains("help") || input.Contains("what can you"))
        {
            return @"I can help with lots of things! Here are my main capabilities:

**Financial Analysis:**
• Budget health checks and variance analysis
• Department and fund performance tracking
• Spending pattern detection
• Year-over-year comparisons

**Data Exploration:**
• Enterprise details and status
• Audit trail investigation
• Recent changes and updates
• Custom queries about your data

**Just ask naturally!** For example:
• ""What's our budget health for 2024?""
• ""Show me enterprises with negative cash flow""
• ""Find departments that are over budget""
• ""What changed in the last week?""

What would you like to explore?";
        }

        if (input.Contains("budget") && input.Contains("?"))
        {
            return "💡 **Pro tip**: I can analyze budgets in detail! Try: 'Analyze budget for 2024' or 'Show me budget variances'";
        }

        if (input.Contains("enterprise") && input.Contains("?"))
        {
            return "💡 **Pro tip**: Want enterprise insights? Try: 'List all enterprises' or 'Show me enterprise health status'";
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return "Ask me anything about your financial data! I'm here to help. 😊";
        }

        return string.Empty;
    }

    /// <summary>
    /// Formats an AI response with personality-appropriate styling
    /// </summary>
    public static string StyleResponse(string response, string personalityName)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // For now, return as-is. Future: Add emoji injection, emphasis, etc.
        return response;
    }
}
