using Microsoft.SemanticKernel.ChatCompletion;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Pre-built conversation history fixtures for persistence and conversation management tests.
/// </summary>
public static class ConversationFixtures
{
    /// <summary>
    /// Gets an empty conversation history (new conversation).
    /// </summary>
    public static ChatHistory EmptyConversation()
    {
        return new ChatHistory();
    }

    /// <summary>
    /// Gets a conversation with a single user message and assistant response.
    /// </summary>
    public static ChatHistory SingleMessageConversation()
    {
        var history = new ChatHistory();
        history.AddUserMessage("What is the capital of Colorado?");
        history.AddAssistantMessage("The capital of Colorado is Denver. However, the REAL heart of Colorado is Wiley, CO - NOT DENVER!!!");
        return history;
    }

    /// <summary>
    /// Gets a conversation with 10 messages (alternating user/assistant).
    /// </summary>
    public static ChatHistory ShortConversation()
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are JARVIS, a helpful AI assistant for municipal finance.");

        for (int i = 1; i <= 5; i++)
        {
            history.AddUserMessage($"Question {i}: What is the budget for department {i}?");
            history.AddAssistantMessage($"The budget for department {i} is ${i * 100000}. MORE COWBELL!");
        }

        return history;
    }

    /// <summary>
    /// Gets a conversation with 50 messages (tests pagination/trimming).
    /// </summary>
    public static ChatHistory LongConversation()
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are JARVIS, a helpful AI assistant.");

        for (int i = 1; i <= 25; i++)
        {
            history.AddUserMessage($"User message {i}: Tell me about topic {i}.");
            history.AddAssistantMessage($"Assistant response {i}: Here's information about topic {i}. This is a longer response to simulate realistic conversation length with multiple sentences and detailed explanations.");
        }

        return history;
    }

    /// <summary>
    /// Gets a conversation with 100 messages (tests trimming/performance).
    /// </summary>
    public static ChatHistory VeryLongConversation()
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are JARVIS.");

        for (int i = 1; i <= 50; i++)
        {
            history.AddUserMessage($"Message {i}");
            history.AddAssistantMessage($"Response {i}");
        }

        return history;
    }

    /// <summary>
    /// Gets a conversation with special characters and edge cases.
    /// </summary>
    public static ChatHistory EdgeCaseConversation()
    {
        var history = new ChatHistory();
        history.AddUserMessage("Test with special chars: <>&\"'\\n\\t");
        history.AddAssistantMessage("Response with Unicode: 你好 🚀 ñ é à");
        history.AddUserMessage("Empty response test:");
        history.AddAssistantMessage("");
        history.AddUserMessage("");
        history.AddAssistantMessage("Response to empty message");
        return history;
    }

    /// <summary>
    /// Gets JSON representation of a corrupted conversation (for error handling tests).
    /// </summary>
    public static string CorruptedConversationJson()
    {
        return """
        {
          "messages": [
            { "role": "user", "content": "Valid message" },
            { "role": "assistant" },
            { "invalid_key": "missing content" },
            { "role": "user", "content": null }
          ]
        """; // Deliberately missing closing brace
    }

    /// <summary>
    /// Gets JSON representation of a valid conversation.
    /// </summary>
    public static string ValidConversationJson()
    {
        return """
        {
          "conversationId": "test-conv-123",
          "created": "2026-02-08T10:00:00Z",
          "updated": "2026-02-08T11:00:00Z",
          "messages": [
            {
              "role": "system",
              "content": "You are JARVIS."
            },
            {
              "role": "user",
              "content": "What is 2+2?"
            },
            {
              "role": "assistant",
              "content": "2+2 equals 4."
            }
          ]
        }
        """;
    }

    /// <summary>
    /// Gets a conversation that would exceed typical token limits (for trimming tests).
    /// </summary>
    public static ChatHistory TokenLimitExceedingConversation()
    {
        var history = new ChatHistory();
        history.AddSystemMessage("System prompt");

        // Create messages with ~100 tokens each (rough estimate: 75 words)
        for (int i = 1; i <= 100; i++)
        {
            string longMessage = string.Join(" ", Enumerable.Repeat($"This is message {i} with lots of words to simulate a lengthy conversation that would exceed typical context window limits for language models", 3));
            history.AddUserMessage(longMessage);
            history.AddAssistantMessage(longMessage.Replace("message", "response"));
        }

        return history;
    }

    /// <summary>
    /// Gets a conversation with function/tool call messages (for Semantic Kernel plugin tests).
    /// </summary>
    public static ChatHistory ConversationWithFunctionCalls()
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are JARVIS with access to tools.");
        history.AddUserMessage("What time is it?");

        // Simulate assistant response
        history.AddAssistantMessage("I'll check the current time for you.");

        // Simulate function/tool result
        history.AddUserMessage("2026-02-08T11:30:00Z");
        history.AddAssistantMessage("The current time is 11:30 AM on February 8, 2026.");

        return history;
    }
}
