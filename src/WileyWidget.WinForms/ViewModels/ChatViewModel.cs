using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using IActivityLogRepository = WileyWidget.Business.Interfaces.IActivityLogRepository;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for chat functionality, providing MVVM separation for ChatPanel.
/// Handles message sending, conversation management, and AI interactions.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ChatViewModel : ObservableObject
{
    private readonly ILogger<ChatViewModel> _logger;
    private readonly IAIService _aiService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IAIContextExtractionService? _contextExtractionService;
    private readonly IActivityLogRepository? _activityLogRepository;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> messages = new();

    [ObservableProperty]
    private string status = "Ready";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string currentConversationId = string.Empty;

    private List<ChatMessage> _conversationHistory = new();

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    public ChatViewModel(
        ILogger<ChatViewModel> logger,
        IAIService aiService,
        IConversationRepository conversationRepository,
        IAIContextExtractionService? contextExtractionService = null,
        IActivityLogRepository? activityLogRepository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        _contextExtractionService = contextExtractionService;
        _activityLogRepository = activityLogRepository;

        _logger.LogInformation("ChatViewModel initialized");
    }

    /// <summary>
    /// Command to send a message.
    /// </summary>
    [RelayCommand]
    public async Task SendMessageAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || IsBusy)
            return;

        try
        {
            IsBusy = true;
            Status = "Processing message...";

            if (string.IsNullOrEmpty(CurrentConversationId))
            {
                CurrentConversationId = Guid.NewGuid().ToString();
            }

            // Add user message
            var userChatMessage = new ChatMessage
            {
                IsUser = true,
                Message = userMessage,
                Timestamp = DateTime.Now
            };
            Messages.Add(userChatMessage);
            _conversationHistory.Add(userChatMessage);

            // Process AI response
            var response = await ProcessAIResponse(userMessage);

            if (!string.IsNullOrEmpty(response))
            {
                var aiMessage = ChatMessage.CreateAIMessage(response);
                Messages.Add(aiMessage);
                _conversationHistory.Add(aiMessage);
            }

            // Post-processing tasks
            await HandlePostMessageTasks(userMessage, response);

            Status = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Process AI response using the service.
    /// </summary>
    private async Task<string?> ProcessAIResponse(string userMessage)
    {
        return await _aiService.SendMessageAsync(userMessage, _conversationHistory);
    }

    /// <summary>
    /// Handle post-message processing tasks.
    /// </summary>
    private async Task HandlePostMessageTasks(string userMessage, string? response)
    {
        // Extract context entities
        await ExtractContextEntitiesAsync(userMessage, response);

        // Log activity
        await LogChatActivityAsync(userMessage);

        // Auto-save conversation
        await SaveConversationAsync();
    }

    /// <summary>
    /// Extract context entities from messages.
    /// </summary>
    private async Task ExtractContextEntitiesAsync(string userMessage, string? response)
    {
        if (_contextExtractionService == null || string.IsNullOrEmpty(CurrentConversationId))
            return;

        try
        {
            await _contextExtractionService.ExtractEntitiesAsync(userMessage, CurrentConversationId);
            if (!string.IsNullOrEmpty(response))
            {
                await _contextExtractionService.ExtractEntitiesAsync(response, CurrentConversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Context extraction failed: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Log successful chat activity.
    /// </summary>
    private async Task LogChatActivityAsync(string userMessage)
    {
        if (_activityLogRepository == null) return;

        try
        {
            var preview = userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;

            await _activityLogRepository.LogActivityAsync(new WileyWidget.Models.ActivityLog
            {
                ActivityType = "ChatMessage",
                Activity = "AI Chat Interaction",
                Details = $"User: {preview}",
                User = Environment.UserName,
                Status = "Success",
                EntityType = "Conversation",
                EntityId = CurrentConversationId,
                Severity = "Info"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log chat activity: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Set context for the conversation.
    /// </summary>
    public void SetContext(string contextDescription, Dictionary<string, object>? contextData = null)
    {
        try
        {
            _logger.LogInformation("Setting chat context: {Context}", contextDescription);

            var contextMessage = new ChatMessage
            {
                IsUser = false,
                Message = $"Context: {contextDescription}",
                Timestamp = DateTime.UtcNow
            };

            if (contextData != null)
            {
                foreach (var kv in contextData)
                    contextMessage.Metadata[kv.Key] = kv.Value;
            }

            _conversationHistory.Add(contextMessage);
            Status = $"Context: {contextDescription}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting chat context");
        }
    }

    /// <summary>
    /// Clear the conversation.
    /// </summary>
    [RelayCommand]
    public void ClearConversation()
    {
        Messages.Clear();
        _conversationHistory.Clear();
        CurrentConversationId = string.Empty;
        Status = "Conversation cleared";
        _logger.LogInformation("Conversation cleared");
    }

    /// <summary>
    /// Load a conversation by ID.
    /// </summary>
    public async Task LoadConversationAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;

        try
        {
            Status = $"Loading conversation {conversationId}...";
            _logger.LogInformation("Loading conversation: {ConversationId}", conversationId);

            var conversationObj = await _conversationRepository.GetConversationAsync(conversationId);

            if (conversationObj == null || conversationObj is not ConversationHistory conversation)
            {
                Status = $"Conversation {conversationId} not found";
                _logger.LogWarning("Conversation not found: {ConversationId}", conversationId);
                return;
            }

            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.MessagesJson);
            if (messages != null && messages.Count > 0)
            {
                _conversationHistory.Clear();
                _conversationHistory.AddRange(messages);

                Messages.Clear();
                foreach (var msg in messages)
                {
                    Messages.Add(msg);
                }
            }

            CurrentConversationId = conversationId;
            Status = $"Loaded conversation: {conversation.Title}";
            _logger.LogInformation("Conversation loaded successfully: {ConversationId}, MessageCount: {Count}",
                conversationId, messages?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation: {ConversationId}", conversationId);
            Status = $"Error loading conversation: {ex.Message}";
        }
    }

    /// <summary>
    /// Save the current conversation.
    /// </summary>
    public async Task SaveConversationAsync(string? conversationId = null)
    {
        if (_conversationHistory.Count == 0)
        {
            Status = "No messages to save";
            return;
        }

        try
        {
            Status = "Saving conversation...";

            var saveId = conversationId ?? CurrentConversationId ?? Guid.NewGuid().ToString();
            _logger.LogInformation("Saving conversation: {ConversationId}", saveId);

            var messagesJson = JsonSerializer.Serialize(_conversationHistory);

            var conversation = new ConversationHistory
            {
                ConversationId = saveId,
                Title = $"Chat - {DateTime.Now:yyyy-MM-dd HH:mm}",
                MessagesJson = messagesJson,
                MessageCount = _conversationHistory.Count,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _conversationRepository.SaveConversationAsync(conversation);
            CurrentConversationId = saveId;

            Status = "Conversation saved successfully";
            _logger.LogInformation("Conversation saved: {ConversationId}, MessageCount: {Count}",
                saveId, _conversationHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation: {ConversationId}", conversationId);
            Status = $"Error saving conversation: {ex.Message}";
        }
    }

    /// <summary>
    /// Get recent conversations.
    /// </summary>
    public async Task<List<ConversationHistory>> GetRecentConversationsAsync(int limit = 20)
    {
        try
        {
            var conversations = await _conversationRepository.GetConversationsAsync(0, limit);
            return conversations?.OfType<ConversationHistory>().ToList() ?? new List<ConversationHistory>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent conversations: {Error}", ex.Message);
            return new List<ConversationHistory>();
        }
    }

    /// <summary>
    /// Start a new conversation.
    /// </summary>
    [RelayCommand]
    public void StartNewConversation()
    {
        ClearConversation();
        CurrentConversationId = string.Empty;
        Status = "Started new conversation";
        _logger.LogInformation("New conversation started");
    }
}
