using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.ViewModels;

/// <summary>
/// ViewModel for ChatPanel providing comprehensive AI chat interaction with conversation management.
/// Supports message history, conversation persistence, context extraction, and activity logging.
/// Follows modern MVVM pattern with CommunityToolkit.Mvvm for WinForms data binding.
/// </summary>
public partial class ChatPanelViewModel : ViewModelBase, IDisposable
{
    #region Dependencies

    private readonly IAIService _aiService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IAIContextExtractionService? _contextExtractionService;
    private readonly IActivityLogRepository? _activityLogRepository;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _disposed;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collection of chat messages displayed in the UI.
    /// Bound to the chat control's message display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    /// <summary>
    /// Collection of recent conversations for history sidebar.
    /// Bound to the conversations list/grid.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ConversationHistory> _conversations = new();

    /// <summary>
    /// Currently selected conversation from the history list.
    /// </summary>
    [ObservableProperty]
    private ConversationHistory? _selectedConversation;

    /// <summary>
    /// Current user input text from the message input box.
    /// Two-way bound to the input TextBox.
    /// </summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// Search text for filtering conversation history.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Filtered collection of conversations based on search text.
    /// Bound to the conversations grid for display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ConversationHistory> _filteredConversations = new();

    /// <summary>
    /// Indicates whether a message is currently being processed.
    /// Controls loading overlay visibility and button states.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Status text displayed in the status bar.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Ready";

    /// <summary>
    /// Error message to display if an operation fails.
    /// Shown in status bar or error panel when not null.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Number of messages in the current conversation.
    /// Displayed in the summary panel.
    /// </summary>
    [ObservableProperty]
    private int _messageCount;

    /// <summary>
    /// Number of total conversations in history.
    /// Displayed in the summary panel.
    /// </summary>
    [ObservableProperty]
    private int _conversationCount;

    /// <summary>
    /// Current conversation ID (null for new conversations).
    /// </summary>
    [ObservableProperty]
    private string? _currentConversationId;

    /// <summary>
    /// Current conversation title.
    /// </summary>
    [ObservableProperty]
    private string _conversationTitle = "New Conversation";

    /// <summary>
    /// Context description for the current conversation.
    /// Displayed in the header area.
    /// </summary>
    [ObservableProperty]
    private string? _contextDescription;

    /// <summary>
    /// Indicates if the conversations list is visible (sidebar expanded).
    /// </summary>
    [ObservableProperty]
    private bool _showConversationsList = true;

    /// <summary>
    /// Last updated timestamp for the current conversation.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastUpdated;

    #endregion

    #region Constants

    private const int MaxConversationsToLoad = 50;
    private const int MaxMessagesInConversation = 500;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes the ChatPanel ViewModel with required services.
    /// </summary>
    /// <param name="aiService">AI service for message processing (required)</param>
    /// <param name="conversationRepository">Repository for conversation persistence (required)</param>
    /// <param name="logger">Logger instance (required)</param>
    /// <param name="contextExtractionService">Optional context extraction service</param>
    /// <param name="activityLogRepository">Optional activity logging repository</param>
    public ChatPanelViewModel(
        IAIService aiService,
        IConversationRepository conversationRepository,
        ILogger<ChatPanelViewModel> logger,
        IAIContextExtractionService? contextExtractionService = null,
        IActivityLogRepository? activityLogRepository = null)
        : base(logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        _contextExtractionService = contextExtractionService;
        _activityLogRepository = activityLogRepository;

        Logger.LogInformation("ChatPanelViewModel initialized with context extraction: {HasContext}, activity logging: {HasLogging}",
            _contextExtractionService != null, _activityLogRepository != null);

        // Initialize with sample data for design-time preview
        InitializeSampleData();
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to send a user message and get AI response.
    /// Validates input, processes message, updates history, and auto-saves.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
            return;

        var userMessage = InputText.Trim();
        InputText = string.Empty; // Clear input immediately

        await _operationLock.WaitAsync();
        try
        {
            await ProcessMessageAsync(userMessage);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Determines if the send message command can execute.
    /// </summary>
    private bool CanSendMessage() => !string.IsNullOrWhiteSpace(InputText) && !IsLoading;

    /// <summary>
    /// Command to load and refresh recent conversations from the repository.
    /// </summary>
    [RelayCommand]
    private async Task LoadConversationsAsync()
    {
        IsLoading = true;
        StatusText = "Loading conversations...";
        ErrorMessage = null;

        try
        {
            Logger.LogInformation("Loading recent conversations");

            var conversationsList = await _conversationRepository.GetConversationsAsync(0, MaxConversationsToLoad);
            var conversations = conversationsList?.OfType<ConversationHistory>().ToList() ?? new List<ConversationHistory>();

            Conversations.Clear();
            foreach (var conv in conversations.OrderByDescending(c => c.UpdatedAt))
            {
                Conversations.Add(conv);
            }

            ConversationCount = Conversations.Count;
            ApplyConversationFilter();

            Logger.LogInformation("Loaded {Count} conversations", Conversations.Count);
            StatusText = $"Loaded {Conversations.Count} conversations";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load conversations");
            ErrorMessage = $"Failed to load conversations: {ex.Message}";
            StatusText = "Error loading conversations";

            // Use sample data as fallback
            LoadSampleConversations();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to load a specific conversation from history.
    /// </summary>
    [RelayCommand]
    private async Task LoadConversationAsync(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        IsLoading = true;
        StatusText = $"Loading conversation...";
        ErrorMessage = null;

        try
        {
            Logger.LogInformation("Loading conversation: {ConversationId}", conversationId);

            var conversationObj = await _conversationRepository.GetConversationAsync(conversationId);
            if (conversationObj is not ConversationHistory conversation)
            {
                ErrorMessage = "Conversation not found";
                StatusText = "Ready";
                return;
            }

            // Deserialize messages
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.MessagesJson ?? "[]") ?? new List<ChatMessage>();

            Messages.Clear();
            foreach (var msg in messages)
            {
                Messages.Add(msg);
            }

            CurrentConversationId = conversationId;
            ConversationTitle = conversation.Title ?? "Untitled Conversation";
            MessageCount = Messages.Count;
            LastUpdated = conversation.UpdatedAt;

            Logger.LogInformation("Loaded conversation with {Count} messages", Messages.Count);
            StatusText = $"Loaded: {ConversationTitle}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load conversation: {ConversationId}", conversationId);
            ErrorMessage = $"Failed to load conversation: {ex.Message}";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to save the current conversation to the repository.
    /// </summary>
    [RelayCommand]
    private async Task SaveConversationAsync()
    {
        if (Messages.Count == 0)
        {
            StatusText = "No messages to save";
            return;
        }

        IsLoading = true;
        StatusText = "Saving conversation...";
        ErrorMessage = null;

        try
        {
            var conversationId = CurrentConversationId ?? Guid.NewGuid().ToString();
            var messagesJson = JsonSerializer.Serialize(Messages.ToList());

            var conversation = new ConversationHistory
            {
                ConversationId = conversationId,
                Title = ConversationTitle,
                MessagesJson = messagesJson,
                MessageCount = Messages.Count,
                CreatedAt = LastUpdated ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _conversationRepository.SaveConversationAsync(conversation);

            CurrentConversationId = conversationId;
            LastUpdated = conversation.UpdatedAt;

            Logger.LogInformation("Saved conversation: {ConversationId} with {Count} messages", conversationId, Messages.Count);
            StatusText = "Conversation saved";

            // Refresh conversations list
            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save conversation");
            ErrorMessage = $"Failed to save conversation: {ex.Message}";
            StatusText = "Save failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to delete a conversation from history.
    /// </summary>
    [RelayCommand]
    private async Task DeleteConversationAsync(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return;

        IsLoading = true;
        StatusText = "Deleting conversation...";
        ErrorMessage = null;

        try
        {
            await _conversationRepository.DeleteConversationAsync(conversationId);

            Logger.LogInformation("Deleted conversation: {ConversationId}", conversationId);
            StatusText = "Conversation deleted";

            // Clear current conversation if it was deleted
            if (CurrentConversationId == conversationId)
            {
                StartNewConversation();
            }

            // Refresh conversations list
            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete conversation: {ConversationId}", conversationId);
            ErrorMessage = $"Failed to delete conversation: {ex.Message}";
            StatusText = "Delete failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to start a new conversation (clears current messages).
    /// </summary>
    [RelayCommand]
    private void NewConversation()
    {
        StartNewConversation();
        StatusText = "Started new conversation";
        Logger.LogInformation("New conversation started");
    }

    /// <summary>
    /// Command to clear all messages in the current conversation.
    /// </summary>
    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
        MessageCount = 0;
        ErrorMessage = null;
        StatusText = "Messages cleared";
        Logger.LogInformation("Messages cleared");
    }

    /// <summary>
    /// Command to refresh the panel (reload conversations and current data).
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusText = "Refreshing...";
        ErrorMessage = null;

        try
        {
            await LoadConversationsAsync();
            StatusText = "Refreshed";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Refresh failed");
            ErrorMessage = "Refresh failed";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to toggle the conversations list visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleConversationsList()
    {
        ShowConversationsList = !ShowConversationsList;
        Logger.LogDebug("Conversations list visibility toggled: {Visible}", ShowConversationsList);
    }

    /// <summary>
    /// Command to export the current conversation to a file.
    /// </summary>
    [RelayCommand]
    private async Task ExportConversationAsync()
    {
        if (Messages.Count == 0)
        {
            ErrorMessage = "No messages to export";
            return;
        }

        IsLoading = true;
        StatusText = "Exporting conversation...";

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"Conversation_{timestamp}.json";
            var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            var exportData = new
            {
                ConversationId = CurrentConversationId,
                Title = ConversationTitle,
                ExportedAt = DateTime.UtcNow,
                MessageCount = Messages.Count,
                Messages = Messages.ToList()
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(filePath, json);

            Logger.LogInformation("Exported conversation to: {FilePath}", filePath);
            StatusText = $"Exported to {fileName}";

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export conversation");
            ErrorMessage = "Export failed";
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set conversation context from external source (e.g., selected account, budget period).
    /// Adds a system message to provide context to the AI.
    /// </summary>
    /// <param name="contextDescription">Human-readable context description</param>
    /// <param name="contextData">Optional structured context data</param>
    public void SetContext(string contextDescription, Dictionary<string, object>? contextData = null)
    {
        try
        {
            Logger.LogInformation("Setting conversation context: {Context}", contextDescription);

            ContextDescription = contextDescription;

            // Add context as a system message
            var contextMessage = new ChatMessage
            {
                IsUser = false,
                Message = $"📋 Context: {contextDescription}",
                Timestamp = DateTime.UtcNow
            };

            if (contextData != null)
            {
                foreach (var kv in contextData)
                {
                    contextMessage.Metadata[kv.Key] = kv.Value;
                }
            }

            Messages.Add(contextMessage);
            MessageCount = Messages.Count;

            StatusText = $"Context set: {contextDescription}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set context");
        }
    }

    /// <summary>
    /// Initialize the view model (load initial data).
    /// Called when the panel is first shown.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            Logger.LogInformation("Initializing ChatPanelViewModel");
            await LoadConversationsAsync();
            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Initialization failed");
            ErrorMessage = "Initialization failed";
            LoadSampleConversations();
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Process a user message: send to AI, add responses, extract context, log activity, auto-save.
    /// </summary>
    private async Task ProcessMessageAsync(string userMessage)
    {
        var startTime = DateTime.UtcNow;
        IsLoading = true;
        StatusText = "Processing message...";
        ErrorMessage = null;

        try
        {
            // Ensure conversation ID exists
            if (string.IsNullOrEmpty(CurrentConversationId))
            {
                CurrentConversationId = Guid.NewGuid().ToString();
            }

            // Add user message
            var userMsg = ChatMessage.CreateUserMessage(userMessage);
            Messages.Add(userMsg);
            MessageCount = Messages.Count;

            Logger.LogInformation("Processing user message: {Message}", userMessage);

            // Get AI response
            var response = await _aiService.SendMessageAsync(userMessage, Messages.ToList());

            if (!string.IsNullOrEmpty(response))
            {
                // Add AI response
                var aiMsg = ChatMessage.CreateAIMessage(response);
                Messages.Add(aiMsg);
                MessageCount = Messages.Count;

                Logger.LogInformation("AI response received ({Length} chars)", response.Length);
            }
            else
            {
                Logger.LogWarning("AI service returned empty response");
                var errorMsg = ChatMessage.CreateAIMessage("Sorry, I couldn't generate a response. Please try again.");
                Messages.Add(errorMsg);
                MessageCount = Messages.Count;
            }

            // Extract context entities (background task)
            await ExtractContextEntitiesAsync(userMessage, response);

            // Log activity
            await LogChatActivityAsync(userMessage, startTime);

            // Auto-save conversation
            await AutoSaveConversationAsync();

            // Update last updated timestamp
            LastUpdated = DateTime.UtcNow;

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing message");
            ErrorMessage = $"Error: {ex.Message}";
            StatusText = "Error";

            // Add error message to chat
            var errorMsg = ChatMessage.CreateAIMessage($"⚠️ Error processing message: {ex.Message}");
            Messages.Add(errorMsg);
            MessageCount = Messages.Count;

            // Log error activity
            await LogChatErrorAsync(ex);
        }
        finally
        {
            IsLoading = false;
            TrimMessagesIfNeeded();
        }
    }

    /// <summary>
    /// Extract context entities from messages using the context extraction service.
    /// </summary>
    private async Task ExtractContextEntitiesAsync(string userMessage, string? aiResponse)
    {
        if (_contextExtractionService == null || string.IsNullOrEmpty(CurrentConversationId))
            return;

        try
        {
            await _contextExtractionService.ExtractEntitiesAsync(userMessage, CurrentConversationId);
            if (!string.IsNullOrEmpty(aiResponse))
            {
                await _contextExtractionService.ExtractEntitiesAsync(aiResponse, CurrentConversationId);
            }
            Logger.LogDebug("Context entities extracted successfully");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Context extraction failed");
        }
    }

    /// <summary>
    /// Log successful chat activity.
    /// </summary>
    private async Task LogChatActivityAsync(string userMessage, DateTime startTime)
    {
        if (_activityLogRepository == null)
            return;

        try
        {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var preview = userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage;

            await _activityLogRepository.LogActivityAsync(new WileyWidget.Services.Abstractions.ActivityLog
            {
                ActivityType = "ChatMessage",
                Activity = "AI Chat Interaction",
                Details = $"User: {preview} (Duration: {duration}ms)",
                User = Environment.UserName,
                Status = "Success",
                EntityType = "Conversation",
                EntityId = CurrentConversationId,
                Severity = "Info",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to log chat activity");
        }
    }

    /// <summary>
    /// Log chat error activity.
    /// </summary>
    private async Task LogChatErrorAsync(Exception ex)
    {
        if (_activityLogRepository == null)
            return;

        try
        {
            await _activityLogRepository.LogActivityAsync(new WileyWidget.Services.Abstractions.ActivityLog
            {
                ActivityType = "ChatError",
                Activity = "AI Chat Error",
                Details = $"Error: {ex.Message}",
                User = Environment.UserName,
                Status = "Failed",
                EntityType = "Conversation",
                EntityId = CurrentConversationId,
                Severity = "Error",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception logEx)
        {
            Logger.LogError(logEx, "Failed to log chat error activity");
        }
    }

    /// <summary>
    /// Auto-save conversation after message exchange.
    /// </summary>
    private async Task AutoSaveConversationAsync()
    {
        try
        {
            if (Messages.Count > 0)
            {
                var conversationId = CurrentConversationId ?? Guid.NewGuid().ToString();
                var messagesJson = JsonSerializer.Serialize(Messages.ToList());

                // Generate title from first user message if not set
                if (ConversationTitle == "New Conversation" && Messages.Any(m => m.IsUser))
                {
                    var firstUserMessage = Messages.First(m => m.IsUser).Message;
                    ConversationTitle = firstUserMessage.Length > 50 ? firstUserMessage[..50] + "..." : firstUserMessage;
                }

                var conversation = new ConversationHistory
                {
                    ConversationId = conversationId,
                    Title = ConversationTitle,
                    MessagesJson = messagesJson,
                    MessageCount = Messages.Count,
                    CreatedAt = LastUpdated ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _conversationRepository.SaveConversationAsync(conversation);
                CurrentConversationId = conversationId;

                Logger.LogDebug("Auto-saved conversation: {ConversationId}", conversationId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Auto-save failed");
        }
    }

    /// <summary>
    /// Start a new conversation by clearing current state.
    /// </summary>
    private void StartNewConversation()
    {
        Messages.Clear();
        CurrentConversationId = null;
        ConversationTitle = "New Conversation";
        ContextDescription = null;
        MessageCount = 0;
        LastUpdated = null;
        ErrorMessage = null;
        SelectedConversation = null;
    }

    /// <summary>
    /// Trim messages if conversation exceeds maximum length.
    /// </summary>
    private void TrimMessagesIfNeeded()
    {
        if (Messages.Count <= MaxMessagesInConversation)
            return;

        Logger.LogInformation("Trimming messages: {Current} -> {Max}", Messages.Count, MaxMessagesInConversation);

        while (Messages.Count > MaxMessagesInConversation)
        {
            Messages.RemoveAt(0);
        }

        MessageCount = Messages.Count;
    }

    /// <summary>
    /// Apply search filter to conversations list.
    /// </summary>
    private void ApplyConversationFilter()
    {
        FilteredConversations.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Conversations
            : Conversations.Where(c =>
                (c.Title?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.ConversationId?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var conv in filtered)
        {
            FilteredConversations.Add(conv);
        }
    }

    /// <summary>
    /// Initialize with sample data for design-time preview.
    /// </summary>
    private void InitializeSampleData()
    {
        if (!string.IsNullOrEmpty(CurrentConversationId))
            return; // Already initialized

        try
        {
            Messages.Add(ChatMessage.CreateAIMessage("👋 Welcome to the AI Chat Panel! How can I assist you today?"));
            MessageCount = Messages.Count;

            ConversationTitle = "Sample Conversation";
            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to initialize sample data");
        }
    }

    /// <summary>
    /// Load sample conversations for fallback/design-time.
    /// </summary>
    private void LoadSampleConversations()
    {
        try
        {
            Conversations.Clear();
            Conversations.Add(new ConversationHistory
            {
                ConversationId = "sample-1",
                Title = "Budget Analysis Discussion",
                MessageCount = 15,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            });
            Conversations.Add(new ConversationHistory
            {
                ConversationId = "sample-2",
                Title = "Revenue Trends Query",
                MessageCount = 8,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            });
            Conversations.Add(new ConversationHistory
            {
                ConversationId = "sample-3",
                Title = "Account Reconciliation Help",
                MessageCount = 22,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-7)
            });

            ConversationCount = Conversations.Count;
            ApplyConversationFilter();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load sample conversations");
        }
    }

    #endregion

    #region Property Changed Handlers

    /// <summary>
    /// Handle input text changes to update command availability.
    /// </summary>
    partial void OnInputTextChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Handle search text changes to filter conversations.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyConversationFilter();
    }

    /// <summary>
    /// Handle selected conversation changes to optionally load it.
    /// </summary>
    partial void OnSelectedConversationChanged(ConversationHistory? value)
    {
        if (value != null)
        {
            _ = LoadConversationAsync(value.ConversationId);
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Dispose managed resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _operationLock?.Dispose();
            Logger.LogInformation("ChatPanelViewModel disposed");
        }

        _disposed = true;
    }

    #endregion
}
