using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls;
using WileyWidgetThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

namespace WileyWidget.WinForms.Forms;

/// <summary>
/// ChatWindow provides a dedicated modal form for AI chat interaction.
/// Allows users to have extended conversations with the AI assistant with context from the main application.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class ChatWindow : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatWindow> _logger;
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
    public ChatWindow(IServiceProvider serviceProvider, MainForm mainForm)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));

        // Only set MdiParent if MainForm is in MDI mode AND using MDI for child forms
        // In DockingManager mode, forms are shown as owned windows, not MDI children
        if (mainForm.IsMdiContainer && mainForm.UseMdiMode)
        {
            MdiParent = mainForm;
        }

        _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ChatWindow>>(serviceProvider);
        _aiService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAIService>(serviceProvider);
        _conversationRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConversationRepository>(serviceProvider);

        WileyWidgetThemeColors.ApplyTheme(this);

        // Optional services - may not be available in all configurations
        _contextExtractionService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIContextExtractionService>(serviceProvider);
        _activityLogRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IActivityLogRepository>(serviceProvider);

        _logger.LogInformation("ChatWindow created");
    }

    private void InitializeComponent()
    {
        // Window properties
        Text = "AI Chat Assistant - Wiley Widget";
        Size = new Size(700, 850);
        StartPosition = FormStartPosition.CenterParent;
        Icon = null; // Use default application icon
        MaximizeBox = true;
        MinimizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        // BackColor inherited from SkinManager theme

        // Initialize conversation history
        _conversationHistory = new List<ChatMessage>();

        // === Status Panel (Top) ===
        _statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            // BackColor inherited from SkinManager theme
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 0, 10, 0)
        };

        _statusLabel = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            // Font and ForeColor inherited from SkinManager theme
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _statusPanel.Controls.Add(_statusLabel);

        // === Main Chat Control (Middle - fills remaining space) ===
        try
        {
            var aiAssistantService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAIAssistantService>(_serviceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<AIChatControl>>(_serviceProvider);

            // Resolve optional personality and insights services
            var personalityService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIPersonalityService>(_serviceProvider);
            var insightsService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IFinancialInsightsService>(_serviceProvider);

            _chatControl = new AIChatControl(aiAssistantService, logger, _aiService, personalityService, insightsService)
            {
                Dock = DockStyle.Fill
            };

            // Handle send message from chat control
            _chatControl.MessageSent += async (sender, e) => await HandleMessageSentAsync(e);
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

        // Add controls to form
        Controls.Add(_statusPanel);
        if (_chatControl != null)
        {
            Controls.Add(_chatControl);
        }

        // Keyboard shortcuts
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        _logger.LogInformation("ChatWindow UI initialized");
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
            if (string.IsNullOrWhiteSpace(userMessage))
                return;

            UpdateStatus("Processing message...");

            // Ensure conversation ID exists
            if (string.IsNullOrEmpty(_currentConversationId))
            {
                _currentConversationId = Guid.NewGuid().ToString();
            }

            // Use the IAIService SendMessageAsync which handles conversation history and tool calls
            var response = await _aiService.SendMessageAsync(userMessage, _conversationHistory!);

            if (_chatControl != null && !string.IsNullOrEmpty(response))
            {
                // AIChatControl already added the user message; only add the AI response here
                _chatControl.Messages.Add(ChatMessage.CreateAIMessage(response));
            }

            // Extract context entities from user message and AI response
            if (_contextExtractionService != null && !string.IsNullOrEmpty(_currentConversationId))
            {
                try
                {
                    // Fire-and-forget background context extraction
#pragma warning disable CS4014 // Because this call is not awaited, execution continues
                    Task.Run(async () =>
                    {
                        try
                        {
                            var conversationId = _currentConversationId;
                            await _contextExtractionService.ExtractEntitiesAsync(userMessage, conversationId);
                            if (!string.IsNullOrEmpty(response))
                            {
                                await _contextExtractionService.ExtractEntitiesAsync(response, conversationId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Background context extraction failed");
                        }
                    });
#pragma warning restore CS4014
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to start context extraction task");
                }
            }

            // Log activity
            if (_activityLogRepository != null)
            {
                try
                {
                    var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                    await _activityLogRepository.LogActivityAsync(new WileyWidget.Services.Abstractions.ActivityLog
                    {
                        ActivityType = "ChatMessage",
                        Activity = "AI Chat Interaction",
                        Details = $"User: {userMessage.Substring(0, Math.Min(50, userMessage.Length))}... (Duration: {duration}ms)",
                        User = Environment.UserName,
                        Status = "Success",
                        EntityType = "Conversation",
                        EntityId = _currentConversationId,
                        Severity = "Info"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log chat activity");
                }
            }

            // Signal the control that parent-side processing is finished so it can hide progress
            // and release any locks/semaphores held while waiting for this handler to complete.
            try
            {
                _chatControl?.NotifyProcessingCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify chat control that processing completed");
            }

            // Auto-save conversation after each exchange
            try
            {
                await SaveConversationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-save conversation");
            }

            UpdateStatus("Ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message send");
            UpdateStatus($"Error: {ex.Message}");

            // Log error activity
            if (_activityLogRepository != null)
            {
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
                catch { /* Best effort logging */ }
            }

            MessageBox.Show(
                $"Error processing message: {ex.Message}",
                "Chat Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Pass initial context from the main window (e.g., selected account).
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

                // Attach optional metadata entries into the message's metadata dictionary (Metadata is read-only)
                if (contextData != null)
                {
                    foreach (var kv in contextData)
                        contextMessage.Metadata[kv.Key] = kv.Value;
                }

                _conversationHistory.Add(contextMessage);
            }

            if (_statusLabel != null)
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
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            if (InvokeRequired)
            {
                Invoke(() => _statusLabel.Text = message);
            }
            else
            {
                _statusLabel.Text = message;
            }
        }
    }

    /// <summary>
    /// Clear all conversation history and messages.
    /// </summary>
    public void ClearConversation()
    {
        try
        {
            _conversationHistory?.Clear();
            if (_chatControl != null)
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

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (MdiParent is MainForm mf)
        {
            try
            {
                mf.RegisterAsDockingMDIChild(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register ChatWindow with DockingManager");
            }
        }
    }

    /// <summary>
    /// Load conversation history from database.
    /// </summary>
    public async Task LoadConversationAsync(string conversationId)
    {
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

            // Deserialize messages from JSON
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.MessagesJson);
            if (messages != null)
            {
                _conversationHistory?.Clear();
                _conversationHistory?.AddRange(messages);

                // Update UI
                if (_chatControl != null)
                {
                    _chatControl.Messages.Clear();
                    foreach (var msg in messages)
                    {
                        _chatControl.Messages.Add(msg);
                    }
                }
            }

            _currentConversationId = conversationId;
            UpdateStatus($"Loaded conversation: {conversation.Title}");
            _logger.LogInformation("Conversation loaded successfully: {ConversationId}, MessageCount: {Count}",
                conversationId, messages?.Count ?? 0);
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
    /// Save conversation history to database.
    /// </summary>
    public async Task SaveConversationAsync(string? conversationId = null)
    {
        try
        {
            UpdateStatus("Saving conversation...");

            // Use existing conversation ID or create new one
            var saveId = conversationId ?? _currentConversationId ?? Guid.NewGuid().ToString();
            _logger.LogInformation("Saving conversation: {ConversationId}", saveId);

            if (_conversationHistory == null || _conversationHistory.Count == 0)
            {
                UpdateStatus("No messages to save");
                return;
            }

            // Serialize messages to JSON
            var messagesJson = JsonSerializer.Serialize(_conversationHistory);

            // Create or update conversation
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
            _currentConversationId = saveId;

            UpdateStatus("Conversation saved successfully");
            _logger.LogInformation("Conversation saved: {ConversationId}, MessageCount: {Count}",
                saveId, _conversationHistory.Count);
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
            _logger.LogError(ex, "Error retrieving recent conversations");
            return new List<ConversationHistory>();
        }
    }

    /// <summary>
    /// Delete a conversation from the database (soft delete by archiving).
    /// </summary>
    public async Task DeleteConversationAsync(string conversationId)
    {
        try
        {
            await _conversationRepository.DeleteConversationAsync(conversationId);
            _logger.LogInformation("Conversation archived: {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation: {ConversationId}", conversationId);
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
            _logger.LogError(ex, "Error starting new conversation");
        }
    }

    /// <summary>
    /// Cleanup on form close.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _logger.LogInformation("ChatWindow closing");
            base.OnFormClosing(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ChatWindow closing");
        }
    }

    /// <summary>
    /// Ensure owned disposable fields are cleaned up to avoid CA2213 warnings
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _chatControl?.Dispose();
            }
            catch { /* best-effort dispose */ }

            try
            {
                _statusLabel?.Dispose();
            }
            catch { /* best-effort dispose */ }

            try
            {
                _statusPanel?.Dispose();
            }
            catch { /* best-effort dispose */ }
        }

        base.Dispose(disposing);
    }
}
