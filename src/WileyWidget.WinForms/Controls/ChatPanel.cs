using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// ChatPanel provides AI chat interaction capability within the application.
/// Allows users to have extended conversations with the AI assistant with context from the main application.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public sealed class ChatPanel : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatPanel> _logger;
    private readonly IAIService _aiService;
    private readonly IConversationRepository _conversationRepository;
    private readonly IAIContextExtractionService? _contextExtractionService;
    private readonly IActivityLogRepository? _activityLogRepository;
    private AIChatControl? _chatControl;
    private List<ChatMessage>? _conversationHistory;
    private Label? _statusLabel;
    private Panel? _statusPanel;
    private string? _currentConversationId;

    /// <summary>
    /// Constructor accepting service provider for DI resolution.
    /// </summary>
    public ChatPanel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Resolve required services with null checks
        try
        {
            _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ChatPanel>>(serviceProvider);
            _aiService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAIService>(serviceProvider);
            _conversationRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConversationRepository>(serviceProvider);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to resolve required services for ChatPanel", ex);
        }

        // Optional services - may not be available in all configurations
        _contextExtractionService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIContextExtractionService>(serviceProvider);
        _activityLogRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IActivityLogRepository>(serviceProvider);

        InitializeComponent();
        _logger.LogInformation("ChatPanel created");
    }

    private void InitializeComponent()
    {
        // Panel properties - theme inherited from parent via SkinManager cascade
        Name = "ChatPanel";
        Dock = DockStyle.Fill;
        AutoScroll = false;

        // Initialize conversation history
        _conversationHistory = new List<ChatMessage>();

        // === Status Panel (Top) ===
        _statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 0, 10, 0),
            Name = "ChatStatusPanel",
            AccessibleName = "Chat Status Panel",
            TabIndex = 0
        };

        _statusLabel = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Name = "ChatStatusLabel",
            AccessibleName = "Chat Status Label",
            TabIndex = 1
        };

        _statusPanel.Controls.Add(_statusLabel);

        // === Main Chat Control (Middle - fills remaining space) ===
        // Use DI-resolved AIChatControl for proper dependency injection
        try
        {
            _chatControl = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AIChatControl>(_serviceProvider);
            if (_chatControl != null)
            {
                _chatControl.Dock = DockStyle.Fill;
                _chatControl.Name = "ChatControl";
                _chatControl.AccessibleName = "AI Chat Control";
                _chatControl.TabIndex = 2;

                // Handle send message from chat control
                _chatControl.MessageSent += ChatControl_MessageSent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AIChatControl");
            MessageBox.Show(
                $"Failed to initialize chat control: {ex.Message}",
                "Chat Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        // Add controls to panel
        if (_statusPanel != null)
        {
            Controls.Add(_statusPanel);
        }
        if (_chatControl != null)
        {
            Controls.Add(_chatControl);
        }

        _logger.LogInformation("ChatPanel UI initialized");
    }

    /// <summary>
    /// Event handler for chat control message sent.
    /// </summary>
    private async void ChatControl_MessageSent(object? sender, string e)
    {
        try
        {
            await HandleMessageSentAsync(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in chat message handler");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle message sent from the chat control.
    /// Integrates with IAIService for full conversational flow.
    /// Extracts context entities and logs activity.
    /// </summary>
    private async Task HandleMessageSentAsync(string userMessage)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            if (!ValidateAndPrepareMessage(userMessage))
                return;

            UpdateStatus("Processing message...");

            var response = await ProcessAIResponse(userMessage);
            await HandlePostMessageTasks(userMessage, response, startTime);

            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message send");
            UpdateStatus($"Error: {ex.Message}");

            // Log error activity
            await LogChatErrorAsync(ex);

            MessageBox.Show(
                $"Error processing message: {ex.Message}",
                "Chat Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Validate message and prepare conversation state.
    /// </summary>
    private bool ValidateAndPrepareMessage(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;

        // Ensure conversation ID exists
        if (string.IsNullOrEmpty(_currentConversationId))
        {
            _currentConversationId = Guid.NewGuid().ToString();
        }

        // Validate conversation history exists
        if (_conversationHistory == null)
        {
            _conversationHistory = new List<ChatMessage>();
        }

        return true;
    }

    /// <summary>
    /// Process AI response using the service.
    /// </summary>
    private async Task<string?> ProcessAIResponse(string userMessage)
    {
        if (_aiService == null)
            throw new InvalidOperationException("AI service not available");

        if (_conversationHistory == null)
            throw new InvalidOperationException("Conversation history not initialized");

        return await _aiService.SendMessageAsync(userMessage, _conversationHistory);
    }

    /// <summary>
    /// Handle post-message processing tasks.
    /// </summary>
    private async Task HandlePostMessageTasks(string userMessage, string? response, DateTime startTime)
    {
        if (_chatControl != null && !string.IsNullOrEmpty(response) && !_chatControl.IsDisposed)
        {
            // AIChatControl already added the user message; only add the AI response here
            _chatControl.Messages.Add(ChatMessage.CreateAIMessage(response));
        }

        // Extract context entities from user message and AI response
        await ExtractContextEntitiesAsync(userMessage, response);

        // Log activity
        await LogChatActivityAsync(userMessage, startTime);

        // Signal the control that parent-side processing is finished
        if (_chatControl != null && !_chatControl.IsDisposed)
        {
            _chatControl.NotifyProcessingCompleted();
        }

        // Auto-save conversation after each exchange
        await SaveConversationAsync();
    }

    /// <summary>
    /// Extract context entities from messages in background.
    /// </summary>
    private async Task ExtractContextEntitiesAsync(string userMessage, string? response)
    {
        if (_contextExtractionService == null || string.IsNullOrEmpty(_currentConversationId))
            return;

        try
        {
            await _contextExtractionService.ExtractEntitiesAsync(userMessage, _currentConversationId);
            if (!string.IsNullOrEmpty(response))
            {
                await _contextExtractionService.ExtractEntitiesAsync(response, _currentConversationId);
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
    private async Task LogChatActivityAsync(string userMessage, DateTime startTime)
    {
        if (_activityLogRepository == null) return;

        try
        {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var preview = userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;

            await _activityLogRepository.LogActivityAsync(new WileyWidget.Services.Abstractions.ActivityLog
            {
                ActivityType = "ChatMessage",
                Activity = "AI Chat Interaction",
                Details = $"User: {preview} (Duration: {duration}ms)",
                User = Environment.UserName,
                Status = "Success",
                EntityType = "Conversation",
                EntityId = _currentConversationId,
                Severity = "Info"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log chat activity: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Log chat error activity.
    /// </summary>
    private async Task LogChatErrorAsync(Exception ex)
    {
        if (_activityLogRepository == null) return;

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
                EntityId = _currentConversationId,
                Severity = "Error"
            });
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to log chat error activity");
        }
    }

    /// <summary>
    /// Pass initial context to the chat (e.g., selected account).
    /// This allows personalized AI responses based on the current application state.
    /// </summary>
    public void SetContext(string contextDescription, Dictionary<string, object>? contextData = null)
    {
        try
        {
            _logger.LogInformation("Setting chat context: {Context}", contextDescription);

            if (_conversationHistory != null)
            {
                // Add context as a system message to initialize the conversation
                var contextMessage = new ChatMessage
                {
                    IsUser = false,
                    Message = $"Context: {contextDescription}",
                    Timestamp = DateTime.UtcNow
                };

                // Attach optional metadata entries into the message's metadata dictionary
                if (contextData != null)
                {
                    foreach (var kv in contextData)
                        contextMessage.Metadata[kv.Key] = kv.Value;
                }

                _conversationHistory.Add(contextMessage);
            }

            if (_statusLabel != null && !_statusLabel.IsDisposed)
            {
                _statusLabel.Text = $"Context: {contextDescription}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting chat context");
        }
    }

    /// <summary>
    /// Update the status bar with a message.
    /// Thread-safe: marshals to UI thread if necessary.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (_statusLabel == null || _statusLabel.IsDisposed || IsDisposed) return;

        if (InvokeRequired)
        {
            try
            {
                Invoke(() => UpdateStatus(message));
            }
            catch (ObjectDisposedException)
            {
                // Panel disposed during status update - safe to ignore
            }
            return;
        }

        _statusLabel.Text = message;
    }

    /// <summary>
    /// Clear all conversation history and messages.
    /// </summary>
    public void ClearConversation()
    {
        try
        {
            _conversationHistory?.Clear();
            if (_chatControl != null && !_chatControl.IsDisposed)
            {
                _chatControl.Messages.Clear();
            }
            UpdateStatus("Conversation cleared");
            _logger.LogInformation("Conversation cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation");
        }
    }

    /// <summary>
    /// Load conversation history from database.
    /// </summary>
    public async Task LoadConversationAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;

        try
        {
            UpdateStatus($"Loading conversation {conversationId}...");
            _logger.LogInformation("Loading conversation: {ConversationId}", conversationId);

            var conversationObj = await _conversationRepository.GetConversationAsync(conversationId);

            if (conversationObj == null || conversationObj is not ConversationHistory conversation)
            {
                UpdateStatus($"Conversation {conversationId} not found");
                _logger.LogWarning("Conversation not found: {ConversationId}", conversationId);
                return;
            }

            DeserializeAndLoadMessages(conversation, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation: {ConversationId}", conversationId);
            UpdateStatus($"Error loading conversation: {ex.Message}");
            MessageBox.Show(
                $"Failed to load conversation: {ex.Message}",
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Deserialize messages and update UI.
    /// </summary>
    private void DeserializeAndLoadMessages(ConversationHistory conversation, string conversationId)
    {
        try
        {
            // Deserialize messages from JSON
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.MessagesJson);
            if (messages != null && messages.Count > 0)
            {
                _conversationHistory?.Clear();
                _conversationHistory?.AddRange(messages);

                UpdateUIWithLoadedMessages(messages);
            }

            _currentConversationId = conversationId;
            UpdateStatus($"Loaded conversation: {conversation.Title}");
            _logger.LogInformation("Conversation loaded successfully: {ConversationId}, MessageCount: {Count}",
                conversationId, messages?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing conversation messages: {ConversationId}", conversationId);
            UpdateStatus($"Error loading conversation: {ex.Message}");
        }
    }

    /// <summary>
    /// Update UI with loaded messages.
    /// </summary>
    private void UpdateUIWithLoadedMessages(List<ChatMessage> messages)
    {
        if (_chatControl != null && !_chatControl.IsDisposed)
        {
            _chatControl.Messages.Clear();
            foreach (var msg in messages)
            {
                _chatControl.Messages.Add(msg);
            }
        }
    }

    /// <summary>
    /// Save conversation history to database.
    /// </summary>
    public async Task SaveConversationAsync(string? conversationId = null)
    {
        if (_conversationHistory == null || _conversationHistory.Count == 0)
        {
            UpdateStatus("No messages to save");
            return;
        }

        try
        {
            UpdateStatus("Saving conversation...");

            var conversationData = PrepareConversationData(conversationId);
            await PersistConversation(conversationData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation: {ConversationId}", conversationId);
            UpdateStatus($"Error saving conversation: {ex.Message}");
            MessageBox.Show(
                $"Failed to save conversation: {ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Prepare conversation data for saving.
    /// </summary>
    private ConversationHistory PrepareConversationData(string? conversationId)
    {
        // Use existing conversation ID or create new one
        var saveId = conversationId ?? _currentConversationId ?? Guid.NewGuid().ToString();
        _logger.LogInformation("Saving conversation: {ConversationId}", saveId);

        // Serialize messages to JSON
        var messagesJson = JsonSerializer.Serialize(_conversationHistory);

        return new ConversationHistory
        {
            ConversationId = saveId,
            Title = $"Chat - {DateTime.Now:yyyy-MM-dd HH:mm}",
            MessagesJson = messagesJson,
            MessageCount = _conversationHistory?.Count ?? 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Persist conversation to database.
    /// </summary>
    private async Task PersistConversation(ConversationHistory conversation)
    {
        await _conversationRepository.SaveConversationAsync(conversation);
        _currentConversationId = conversation.ConversationId;

        UpdateStatus("Conversation saved successfully");
        _logger.LogInformation("Conversation saved: {ConversationId}, MessageCount: {Count}",
            conversation.ConversationId, conversation.MessageCount);
    }

    /// <summary>
    /// Get a list of recent conversations from the database.
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
    /// Delete a conversation from the database (soft delete by archiving).
    /// </summary>
    public async Task DeleteConversationAsync(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;

        try
        {
            await _conversationRepository.DeleteConversationAsync(conversationId);
            _logger.LogInformation("Conversation archived: {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation: {ConversationId}, {Error}", conversationId, ex.Message);
        }
    }

    /// <summary>
    /// Start a new conversation (clears current and generates new ID).
    /// </summary>
    public void StartNewConversation()
    {
        try
        {
            ClearConversation();
            _currentConversationId = null;
            UpdateStatus("Started new conversation");
            _logger.LogInformation("New conversation started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting new conversation: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Cleanup panel resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chatControl?.Dispose();
            _statusLabel?.Dispose();
            _statusPanel?.Dispose();
        }

        base.Dispose(disposing);
    }
}
