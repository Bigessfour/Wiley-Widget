using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for AI Chat functionality with tool execution and conversational AI support.
/// Manages chat message history, tool parsing/execution, and optional conversational fallback.
/// Follows MVVM pattern with CommunityToolkit.Mvvm for WinForms data binding.
/// </summary>
public partial class AIChatViewModel : ViewModelBase, IDisposable
{
    #region Dependencies

    private readonly IAIAssistantService _aiService;
    private readonly IAIService? _conversationalAIService;
    private readonly IAIPersonalityService? _personalityService;
    private readonly IFinancialInsightsService? _insightsService;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private bool _disposed;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collection of chat messages displayed in the UI.
    /// Hard limit enforced to prevent unbounded growth in long sessions.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    /// <summary>
    /// Current user input text from the message input box.
    /// </summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// Selected tool from the dropdown (null or empty = auto-detect).
    /// </summary>
    [ObservableProperty]
    private string? _selectedTool;

    /// <summary>
    /// Available tools for selection in the UI.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableTools = new();

    /// <summary>
    /// Indicates whether a message is currently being processed.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Status text displayed in the progress panel.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Ready";

    /// <summary>
    /// Error message to display if an operation fails.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Number of messages currently in the conversation.
    /// </summary>
    [ObservableProperty]
    private int _messageCount;

    /// <summary>
    /// Indicates whether conversational AI fallback is available.
    /// </summary>
    [ObservableProperty]
    private bool _hasConversationalAI;

    /// <summary>
    /// Current personality name for AI responses.
    /// </summary>
    [ObservableProperty]
    private string _currentPersonality = "Professional";

    #endregion

    #region Constants

    private const int MaxMessageCount = 300;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes the AI Chat ViewModel with required services.
    /// </summary>
    /// <param name="aiService">Tool execution service (required)</param>
    /// <param name="logger">Logger instance (required)</param>
    /// <param name="conversationalAIService">Optional conversational AI fallback service</param>
    /// <param name="personalityService">Optional personality management service</param>
    /// <param name="insightsService">Optional financial insights service</param>
    public AIChatViewModel(
        IAIAssistantService aiService,
        ILogger<AIChatViewModel> logger,
        IAIService? conversationalAIService = null,
        IAIPersonalityService? personalityService = null,
        IFinancialInsightsService? insightsService = null)
        : base(logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _conversationalAIService = conversationalAIService;
        _personalityService = personalityService;
        _insightsService = insightsService;

        HasConversationalAI = _conversationalAIService != null;

        // Initialize available tools
        AvailableTools = new ObservableCollection<string>(
            new[] { "Auto-detect tool" }.Concat(
                _aiService.GetAvailableTools().Select(t => $"{t.Name} - {t.Description}")
            )
        );

        SelectedTool = AvailableTools.FirstOrDefault();

        // Get current personality
        if (_personalityService != null)
        {
            try
            {
                CurrentPersonality = _personalityService.CurrentPersonality;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve current personality, using default");
            }
        }

        Logger.LogInformation(
            "AIChatViewModel initialized with conversational AI: {HasConversationalAI}, personality: {Personality}",
            HasConversationalAI, CurrentPersonality);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to send a user message and process AI response.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
            return;

        var input = InputText.Trim();
        InputText = string.Empty; // Clear input immediately

        ErrorMessage = null;
        IsLoading = true;
        StatusText = "Processing message...";

        // Add timeout to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await _executionSemaphore.WaitAsync(cts.Token);
        try
        {
            // Add user message
            var userMessage = new ChatMessage
            {
                IsUser = true,
                Message = input,
                Timestamp = DateTime.Now
            };
            Messages.Add(userMessage);
            MessageCount = Messages.Count;

            Logger.LogInformation("User message received: {Message}", input);

            // Parse for tool call
            var toolCall = _aiService.ParseInputForTool(input);

            string responseMessage;
            if (toolCall != null)
            {
                Logger.LogInformation("Parsed tool call: {ToolName}", toolCall.Name);
                StatusText = $"Executing tool: {toolCall.Name}...";

                // Execute tool
                var result = await _aiService.ExecuteToolAsync(toolCall);

                if (result.IsError)
                {
                    responseMessage = $"Error: {result.ErrorMessage}";
                    Logger.LogWarning("Tool execution failed: {Error}", result.ErrorMessage);
                }
                else
                {
                    // Truncate long responses for display
                    var safeContent = string.IsNullOrEmpty(result.Content) ? "[No content]" : result.Content;
                    var content = safeContent.Length > 1000
                        ? safeContent[..1000] + "\n\n... (truncated for display)"
                        : safeContent;
                    responseMessage = $"Tool: {toolCall.Name}\n{new string('-', 40)}\n{content}";
                    Logger.LogInformation("Tool execution successful, response length: {Length}", content.Length);
                }
            }
            else
            {
                // Use conversational AI fallback if available
                if (_conversationalAIService != null)
                {
                    Logger.LogInformation("No tool detected; attempting conversational AI response");
                    StatusText = "Getting AI insights...";

                    // Use the same timeout CTS
                    try
                    {
                        responseMessage = await _conversationalAIService.GetInsightsAsync(
                            context: "User querying codebase via AI Chat interface. Provide helpful, concise responses.",
                            question: input,
                            cancellationToken: cts.Token);

                        if (string.IsNullOrWhiteSpace(responseMessage))
                        {
                            responseMessage = "No response from AI service. Try a tool command instead.";
                            Logger.LogWarning("XAI service returned empty response");
                        }
                        else
                        {
                            responseMessage = $"AI Insights:\n{responseMessage}";
                            Logger.LogInformation("Conversational AI response received ({Length} chars)", responseMessage.Length);
                        }
                    }
                    catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogError(ioEx, "XAI API key not configured");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(ioEx);
                        ErrorMessage = "AI API key not configured. Set XAI_API_KEY environment variable.";
                    }
                    catch (TaskCanceledException tcEx)
                    {
                        Logger.LogWarning(tcEx, "XAI request timed out after 30 seconds");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(tcEx);
                        ErrorMessage = "AI request timed out after 30 seconds. Please try again.";
                    }
                    catch (HttpRequestException hrEx)
                    {
                        Logger.LogWarning(hrEx, "XAI API network error");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(hrEx);
                        ErrorMessage = "Network error contacting AI service.";
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Conversational AI fallback failed");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(ex);
                        ErrorMessage = "AI service error. Please try again.";
                    }
                }
                else
                {
                    // No conversational AI available; show tool help
                    responseMessage = GetToolHelpMessage();
                    Logger.LogDebug("Conversational AI not configured; showing help message");
                }
            }

            // Add AI response
            var aiMessage = new ChatMessage
            {
                IsUser = false,
                Message = responseMessage,
                Timestamp = DateTime.Now
            };
            Messages.Add(aiMessage);
            MessageCount = Messages.Count;

            // Trim messages if needed
            TrimMessagesIfNeeded();

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending message");
            var friendlyError = ConversationalAIHelper.FormatFriendlyError(ex);
            var errorMessage = new ChatMessage
            {
                IsUser = false,
                Message = friendlyError,
                Timestamp = DateTime.Now
            };
            Messages.Add(errorMessage);
            MessageCount = Messages.Count;
            ErrorMessage = "An error occurred. See message for details.";
            StatusText = "Error";
        }
        finally
        {
            _executionSemaphore.Release();
            IsLoading = false;
        }
    }

    /// <summary>
    /// Determines if the send message command can execute.
    /// </summary>
    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(InputText) && !IsLoading;

    /// <summary>
    /// Command to clear all messages from the conversation.
    /// </summary>
    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
        MessageCount = 0;
        ErrorMessage = null;
        StatusText = "Ready";
        Logger.LogInformation("Chat messages cleared");
    }

    /// <summary>
    /// Command to refresh/reload the chat interface.
    /// Shows welcome message if not already shown.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusText = "Refreshing...";
        try
        {
            // Reload personality if available
            if (_personalityService != null)
            {
                try
                {
                    CurrentPersonality = _personalityService.CurrentPersonality;
                    Logger.LogInformation("Personality refreshed: {Personality}", CurrentPersonality);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to refresh personality");
                }
            }

            // Add welcome message if messages are empty
            if (Messages.Count == 0)
            {
                await ShowWelcomeMessageAsync();
            }

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing chat");
            ErrorMessage = "Failed to refresh. Please try again.";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to change the AI personality.
    /// </summary>
    [RelayCommand]
    private async Task ChangePersonalityAsync(string personality)
    {
        if (_personalityService == null)
        {
            Logger.LogWarning("Personality service not available");
            return;
        }

        try
        {
            _personalityService.SetPersonality(personality);
            CurrentPersonality = personality;
            Logger.LogInformation("Personality changed to: {Personality}", personality);

            // Add system message about personality change
            var systemMessage = new ChatMessage
            {
                IsUser = false,
                Message = $"AI personality changed to: {personality}",
                Timestamp = DateTime.Now
            };
            Messages.Add(systemMessage);
            MessageCount = Messages.Count;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to change personality");
            ErrorMessage = "Failed to change personality.";
        }
    }

    /// <summary>
    /// Command to get financial insights for a specific query.
    /// </summary>
    [RelayCommand]
    private async Task GetFinancialInsightsAsync(string query)
    {
        if (_insightsService == null)
        {
            ErrorMessage = "Financial insights service not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
            return;

        IsLoading = true;
        StatusText = "Getting financial insights...";
        try
        {
            var insights = await _insightsService.GetInsightsAsync(query, CancellationToken.None);

            var aiMessage = new ChatMessage
            {
                IsUser = false,
                Message = $"Financial Insights:\n{insights}",
                Timestamp = DateTime.Now
            };
            Messages.Add(aiMessage);
            MessageCount = Messages.Count;

            Logger.LogInformation("Financial insights retrieved for query: {Query}", query);
            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get financial insights");
            ErrorMessage = "Failed to retrieve financial insights.";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Shows the welcome message in the chat.
    /// </summary>
    private async Task ShowWelcomeMessageAsync()
    {
        try
        {
            string welcomeMessage = ConversationalAIHelper.GetWelcomeMessage(CurrentPersonality);

            var welcomeChatMessage = ChatMessage.CreateAIMessage(welcomeMessage);
            Messages.Add(welcomeChatMessage);
            MessageCount = Messages.Count;

            Logger.LogInformation("Welcome message displayed with {Personality} personality", CurrentPersonality);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to display welcome message");
        }
    }

    /// <summary>
    /// Gets the tool help message when no tool is detected and no conversational AI is available.
    /// </summary>
    private string GetToolHelpMessage()
    {
        var tools = _aiService.GetAvailableTools();
        var toolsList = string.Join("\n", tools.Select(t => $"- {t.Name}: {t.Description}"));

        return $"No tool detected. Conversational AI not configured.\n\n" +
               $"Available tools:\n{toolsList}\n\n" +
               $"To enable AI chat: Set XAI_API_KEY environment variable.";
    }

    /// <summary>
    /// Trims older messages when collection exceeds MaxMessageCount.
    /// </summary>
    private void TrimMessagesIfNeeded()
    {
        try
        {
            if (Messages.Count <= MaxMessageCount)
                return;

            Logger.LogInformation("Trimming messages: current count {Count}, max {Max}", Messages.Count, MaxMessageCount);

            // Remove oldest messages
            while (Messages.Count > MaxMessageCount)
            {
                Messages.RemoveAt(0);
            }

            MessageCount = Messages.Count;
            Logger.LogInformation("Messages trimmed to {Count}", Messages.Count);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to trim messages");
        }
    }

    /// <summary>
    /// Handles changes to the input text to update command can-execute state.
    /// </summary>
    partial void OnInputTextChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _executionSemaphore?.Dispose();
            Logger.LogInformation("AIChatViewModel disposed");
        }

        _disposed = true;
    }

    #endregion
}
