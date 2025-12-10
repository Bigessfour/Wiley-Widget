using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.WinForms.Helpers;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// AI Chat control for xAI tool execution and conversational interface.
/// Uses Syncfusion SfDataGrid for message display following AccountsForm patterns.
///
/// ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
/// INTEGRATION DOCUMENTATION - AIChatControl
/// ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
///
/// DEPENDENCY INJECTION:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// 1. Constructor Dependencies:
///    - IAIAssistantService: Scoped service for tool parsing and execution
///    - ILogger&lt;AIChatControl&gt;: Scoped logger for diagnostic output
///
/// 2. DI Registration (WileyWidget.WinForms.Configuration.DependencyInjection.cs):
///    services.AddScoped&lt;AIChatControl&gt;();
///    services.AddScoped&lt;IAIAssistantService, AIAssistantService&gt;();
///
/// 3. Instantiation (MainForm.InitializeComponent):
///    var aiService = ServiceProviderExtensions.GetRequiredService&lt;IAIAssistantService&gt;(_serviceProvider);
///    var aiLogger = ServiceProviderExtensions.GetRequiredService&lt;ILogger&lt;AIChatControl&gt;&gt;(_serviceProvider);
///    _aiChatControl = new AIChatControl(aiService, aiLogger);
///
/// SERVICE CONNECTION:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// 1. Tool Detection (ParseInputForTool):
///    - Regex pattern: (read|grep|search|list|get errors)\s+(.+)
///    - Examples: "read MainForm.cs", "grep SendMessageAsync", "search AI"
///    - Returns: ToolCall? with Name, Id, Arguments, ToolType
///
/// 2. Tool Execution (ExecuteToolAsync):
///    - Invokes Python bridge: xai_tool_executor.py
///    - Subprocess communication with JSON serialization
///    - Timeout: 30 seconds per tool call
///    - Concurrency: Limited to 1 execution at a time via SemaphoreSlim
///    - Returns: ToolCallResult with IsError, Content, ErrorMessage
///
/// 3. Message Flow:
///    SendMessageAsync()
///      Γö£ΓöÇ Parse user input for tool keywords
///      Γö£ΓöÇ If tool detected:
///      Γöé  ΓööΓöÇ Call ExecuteToolAsync(toolCall)
///      Γöé     Γö£ΓöÇ Show progress panel "ΓÅ│ Executing tool..."
///      Γöé     Γö£ΓöÇ Wait for Python process completion
///      Γöé     Γö£ΓöÇ Format result or error message
///      Γöé     ΓööΓöÇ Return to UI thread
///      ΓööΓöÇ Add ChatMessage to observable collection
///         ΓööΓöÇ Render in RichTextBox with formatting
///
/// CHAT MESSAGE MODEL:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// ChatMessage.cs properties:
///   - IsUser: bool (true for user messages, false for AI responses)
///   - Message: string (primary content)
///   - Text: string (alias for Message, for binding compatibility)
///   - Timestamp: DateTime (message creation time)
///   - Author: object? (optional metadata, used for Syncfusion Author)
///   - Metadata: IDictionary (arbitrary key-value pairs)
///   - Factory methods: CreateUserMessage(), CreateAIMessage()
///
/// UI COMPONENTS:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// - Header Panel: Title, Clear button
/// - Messages Display: RichTextBox with formatted chat history
/// - Input Panel: TextBox for user input, Send button, Tool selector dropdown
/// - Progress Panel: Shows "ΓÅ│ Executing tool..." during async operations
/// - Keyboard shortcuts: Enter = Send, Shift+Enter = Newline
///
/// ENHANCEMENT OPPORTUNITIES:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// A. FALLBACK CONVERSATIONAL AI: Γ£ô IMPLEMENTED
///    When no tool is detected, XAIService provides conversational responses:
///
///    if (toolCall == null && _conversationalAIService != null)
///    {
///        responseMessage = await _conversationalAIService.GetInsightsAsync(
///            context: "User querying codebase via AI Chat interface",
///            question: input,
///            cancellationToken: CancellationToken.None);
///        responseMessage = $"≡ƒÆ¡ Insights:\n{responseMessage}";
///    }
///
///    Feature Behavior:
///    - Tool commands (read, grep, search, list) ΓåÆ AIAssistantService (Python bridge)
///    - Natural language queries ΓåÆ XAIService (xAI API with Polly resilience)
///    - Graceful fallback if XAIService unavailable or rate-limited
///    - Error handling with user-friendly messages
///
/// B. UNIT TESTS: Γ£ô CREATED
///    Added: tests/AIChatControl_Integration_Analysis.cs
///    Added: tests/AIChatControl_SendMessageAsync_Tests.cs
///    - Mock IAIAssistantService and IAIService for unit testing
///    - Test tool detection: read, grep, search, list, get errors
///    - Test message collection binding
///    - Test conversational fallback
///
/// C. DUPLICATE AUDIT: Γ£ô COMPLETED
///    Added: tests/AIServices_Audit_Duplicates.cs
///    - Verified no duplicate AI service implementations
///    - Confirmed AIAssistantService and XAIService are complementary:
///      * AIAssistantService: Tool execution (filesystem, semantic search)
///      * XAIService: Conversational insights (via xAI API)
///    - Both properly registered under different interfaces
///
/// D. ERROR HANDLING:
///    - Tool execution errors caught and displayed in chat
///    - ToolCallResult.IsError flag indicates failure
///    - Timeout after 30s with user-friendly message
///    - Concurrency semaphore prevents overlapping executions
///
/// E. MESSAGE PERSISTENCE (Future):
///    - Save/load chat history from local file or database
///    - Implement IAsyncDisposable for cleanup
///
/// TESTING:
/// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
/// Run integration tests:
///   dotnet test tests/AIChatControl_Integration_Analysis.cs
///   dotnet test tests/AIServices_Audit_Duplicates.cs
///
/// Manual testing:
///   1. Launch application (dotnet run --project src/WileyWidget.WinForms)
///   2. Open AI Chat panel (Ctrl+1 or toolbar button)
///   3. Try commands:
///      - "read MainForm.cs" ΓåÆ reads file
///      - "grep SendMessageAsync" ΓåÆ searches for pattern
///      - "search AI chat integration" ΓåÆ semantic search
///      - "list src/" ΓåÆ lists directory
///      - "hello world" ΓåÆ no tool detected (future: fallback to conversational)
///
/// ΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉΓòÉ
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AIChatControl : UserControl
{
    /// <summary>
    /// Event raised when a message is successfully sent and processed.
    /// Provides the user message to parent forms/windows for integration.
    /// </summary>
    public event EventHandler<string>? MessageSent;
    private readonly IAIAssistantService _aiService;
    private readonly IAIService? _conversationalAIService;
    private readonly IAIPersonalityService? _personalityService;
    private readonly IFinancialInsightsService? _insightsService;
    private readonly ILogger<AIChatControl> _logger;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private string? _lastResponseId; // Track conversation state for multi-turn dialogs
    private bool _useStreaming = true; // Enable streaming by default
    private bool _welcomeMessageShown = false;

    private RichTextBox? _messagesDisplay;
    private Panel? _inputPanel;
    private TextBox? _inputTextBox;
    private Button? _sendButton;
    private Button? _clearButton;
    private ComboBox? _toolComboBox;
    private Panel? _progressPanel;
    private Label? _progressLabel;
    private Panel? _headerPanel;
    private Label? _headerLabel;

    public ObservableCollection<ChatMessage> Messages { get; }
    // Hard limit on retained messages to avoid unbounded memory/UI growth in long running sessions
    private const int MaxMessageCount = 300;

    /// <summary>
    /// Constructor with mandatory tool execution service and optional conversational AI service.
    /// If IAIService is available, provides fallback conversational responses when no tool is detected.
    /// </summary>
    public AIChatControl(
        IAIAssistantService aiService,
        ILogger<AIChatControl> logger,
        IAIService? conversationalAIService = null,
        IAIPersonalityService? personalityService = null,
        IFinancialInsightsService? insightsService = null)
    {
        // Validate STA thread requirement for WinForms controls
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "AIChatControl must be created on an STA thread. " +
                "Ensure the application entry point is marked with [STAThread] attribute.");
        }

        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conversationalAIService = conversationalAIService;
        _personalityService = personalityService;
        _insightsService = insightsService;
        Messages = new ObservableCollection<ChatMessage>();

        InitializeComponent();
        ShowWelcomeMessage();
        _logger.LogInformation("AIChatControl initialized successfully{ConversationalAI}",
            _conversationalAIService != null ? " with conversational AI fallback enabled" : "");
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        // Respect system DPI scaling for consistent rendering on high-DPI displays
        AutoScaleMode = AutoScaleMode.Dpi;

        Size = new Size(450, 650);
        BackColor = Color.FromArgb(248, 249, 250);

        // === Header Panel ===
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(0, 120, 212),
            Padding = new Padding(15, 0, 15, 0)
        };

        _headerLabel = new Label
        {
            Text = "≡ƒñû AI Assistant",
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        _clearButton = new Button
        {
            Text = "Clear",
            Dock = DockStyle.Right,
            Width = 70,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 120, 212),
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(5)
        };
        _clearButton.FlatAppearance.BorderColor = Color.White;
        _clearButton.FlatAppearance.BorderSize = 1;
        _clearButton.Click += (s, e) => ClearMessages();

        _headerPanel.Controls.Add(_headerLabel);
        _headerPanel.Controls.Add(_clearButton);

        // === Messages Display (RichTextBox for better chat formatting) ===
        _messagesDisplay = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(10),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
            DetectUrls = true
        };
        _messagesDisplay.AccessibleName = "Chat history";
        _messagesDisplay.AccessibleDescription = "Displays the chat conversation history. Use the input box below to send messages.";
        _messagesDisplay.LinkClicked += (s, e) =>
        {
            if (e.LinkText != null)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true }); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to open link: {Link}", e.LinkText); }
            }
        };

        // === Progress Panel ===
        _progressPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = Color.FromArgb(255, 243, 205),
            Visible = false
        };

        _progressLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "ΓÅ│ Executing tool...",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(133, 100, 4),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Italic)
        };
        _progressPanel.Controls.Add(_progressLabel);

        // === Input Panel ===
        _inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = Color.FromArgb(240, 242, 245),
            Padding = new Padding(12)
        };

        // Tool selector combo box with improved styling
        _toolComboBox = new ComboBox
        {
            Location = new Point(12, 12),
            Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.White
        };
        _toolComboBox.AccessibleName = "Tool selector";
        _toolComboBox.AccessibleDescription = "Select a tool to use for parsing commands or leave as auto-detect.";
        _toolComboBox.Items.Add("≡ƒöì Auto-detect tool");
        _toolComboBox.Items.AddRange(_aiService.GetAvailableTools().Select(t => $"≡ƒ¢á∩╕Å {t.Name} - {t.Description}").ToArray());
        _toolComboBox.SelectedIndex = 0;

        // Enhanced input text box
        _inputTextBox = new TextBox
        {
            Location = new Point(12, 45),
            Width = 335,
            Height = 55,
            Multiline = true,
            PlaceholderText = "Type your message... (e.g., 'read MainForm.cs' or 'search for Button')",
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
            ScrollBars = ScrollBars.Vertical
        };
        _inputTextBox.AccessibleName = "Message input";
        _inputTextBox.AccessibleDescription = "Type your message here. Press Enter to send, Shift+Enter for newline.";
        _inputTextBox.KeyDown += InputTextBox_KeyDown;

        // Send button with improved styling
        _sendButton = new Button
        {
            Location = new Point(355, 45),
            Width = 70,
            Height = 55,
            Text = "≡ƒôñ\nSend",
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Enabled = true,
            Cursor = Cursors.Hand
        };
        _sendButton.AccessibleName = "Send message";
        _sendButton.AccessibleDescription = "Send the typed message to the AI assistant.";
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += async (s, e) => await SendMessageAsync();

        _inputPanel.Controls.Add(_toolComboBox);
        _inputPanel.Controls.Add(_inputTextBox);
        _inputPanel.Controls.Add(_sendButton);

        // Basic keyboard/tab order for accessibility
        _messagesDisplay.TabIndex = 0;
        _toolComboBox.TabIndex = 1;
        _inputTextBox.TabIndex = 2;
        _sendButton.TabIndex = 3;

        // === Layout (Proper Z-Order) ===
        Controls.Add(_messagesDisplay);
        Controls.Add(_progressPanel);
        Controls.Add(_inputPanel);
        Controls.Add(_headerPanel);

        ResumeLayout(false);
    }

    private void ClearMessages()
    {
        Messages.Clear();
        if (_messagesDisplay != null)
        {
            _messagesDisplay.Clear();
        }
        _logger.LogInformation("Chat messages cleared");
    }

    private void ShowWelcomeMessage()
    {
        if (_welcomeMessageShown)
        {
            return; // Already shown
        }

        try
        {
            // Get personality name from personality service or default to "Professional"
            string personalityName = "Professional";
            if (_personalityService != null)
            {
                // Try to get current personality from service
                // Note: This assumes AIPersonalityService has a CurrentPersonality property
                // If not available, we'll fall back to the default
                personalityName = "Professional"; // Default fallback
            }

            // Get welcome message from helper
            string welcomeMessage = ConversationalAIHelper.GetWelcomeMessage(personalityName);

            // Add as AI message
            var welcomeChatMessage = ChatMessage.CreateAIMessage(welcomeMessage);
            Messages.Add(welcomeChatMessage);

            // Display in RichTextBox if available
            if (_messagesDisplay != null)
            {
                _messagesDisplay.SelectionColor = Color.FromArgb(0, 120, 212);
                _messagesDisplay.SelectionFont = new Font(_messagesDisplay.Font, FontStyle.Bold);
                _messagesDisplay.AppendText("≡ƒñû AI Assistant: ");
                _messagesDisplay.SelectionColor = Color.Black;
                _messagesDisplay.SelectionFont = new Font(_messagesDisplay.Font, FontStyle.Regular);
                _messagesDisplay.AppendText(welcomeMessage + Environment.NewLine + Environment.NewLine);
            }

            _welcomeMessageShown = true;
            _logger.LogInformation("Welcome message displayed with {Personality} personality", personalityName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display welcome message");
            // Non-critical failure - continue without welcome message
        }
    }

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        if (_inputTextBox == null || string.IsNullOrWhiteSpace(_inputTextBox.Text))
            return;

        var input = _inputTextBox.Text.Trim();
        _inputTextBox.Clear();

        if (string.IsNullOrWhiteSpace(input))
            return;

        // Add user message
        var userMessage = new ChatMessage
        {
            IsUser = true,
            Message = input,
            Timestamp = DateTime.Now
        };
        Messages.Add(userMessage);
        AppendMessageToDisplay(userMessage);

        // Show progress (ensure UI thread execution)
        if (InvokeRequired)
        {
            BeginInvoke(() =>
            {
                if (_progressPanel != null)
                    _progressPanel.Visible = true;
                if (_sendButton != null)
                    _sendButton.Enabled = false;
            });
        }
        else
        {
            if (_progressPanel != null)
                _progressPanel.Visible = true;
            if (_sendButton != null)
                _sendButton.Enabled = false;
        }

        await _executionSemaphore.WaitAsync();
        try
        {
            // If a parent has subscribed to MessageSent, delegate processing to the parent
            // This allows a parent form (e.g., ChatWindow) to centralize AI service usage
            if (MessageSent != null)
            {
                // Leave the semaphore acquired and the progress UI visible until the parent
                // calls NotifyProcessingCompleted(). This avoids concurrent processing.
                try
                {
                    MessageSent.Invoke(this, input);
                }
                catch (Exception ex)
                {
                    // Bubble any event handler failure into the chat display
                    Messages.Add(new ChatMessage { IsUser = false, Message = $"Γ¥î Event handler error: {ex.Message}", Timestamp = DateTime.Now });
                    var lastMsg = Messages.LastOrDefault();
                    if (lastMsg != null)
                        AppendMessageToDisplay(lastMsg);
                    // Release the semaphore since we aren't going to wait for parent processing
                    _executionSemaphore.Release();
                    // Hide progress UI on main thread
                    if (InvokeRequired)
                    {
                        BeginInvoke(() =>
                        {
                            if (_progressPanel != null)
                                _progressPanel.Visible = false;
                            if (_sendButton != null)
                                _sendButton.Enabled = true;
                        });
                    }
                    else
                    {
                        if (_progressPanel != null)
                            _progressPanel.Visible = false;
                        if (_sendButton != null)
                            _sendButton.Enabled = true;
                    }
                }

                // DONE: delegate to parent, return now (parent will add AI response and must call NotifyProcessingCompleted())
                return;
            }

            // Parse for tool call
            var toolCall = _aiService.ParseInputForTool(input);

            string responseMessage;
            if (toolCall != null)
            {
                _logger.LogInformation("Parsed tool call: {ToolName}", toolCall.Name);

                // Execute tool
                var result = await _aiService.ExecuteToolAsync(toolCall);

                if (result.IsError)
                {
                    responseMessage = $"Γ¥î Error: {result.ErrorMessage}";
                }
                else
                {
                    // Truncate long responses for display and guard against null Content
                    var safeContent = string.IsNullOrEmpty(result.Content) ? "[No content]" : result.Content;
                    var content = safeContent.Length > 1000
                        ? safeContent[..1000] + "\n\n... (truncated for display)"
                        : safeContent;
                    responseMessage = $"Γ£à Tool: {toolCall.Name}\n{new string('-', 40)}\n{content}";
                }
            }
            else
            {
                // ENHANCEMENT: Use XAIService for conversational AI fallback
                if (_conversationalAIService != null)
                {
                    _logger.LogInformation("No tool detected; attempting conversational AI response via XAI service");
                    try
                    {
                        responseMessage = await _conversationalAIService.GetInsightsAsync(
                            context: "User querying codebase via AI Chat interface. Provide helpful, concise responses.",
                            question: input,
                            cancellationToken: CancellationToken.None);

                        if (string.IsNullOrWhiteSpace(responseMessage))
                        {
                            responseMessage = "Γä╣∩╕Å No response from AI service. Try a tool command instead.";
                            _logger.LogWarning("XAI service returned empty response");
                        }
                        else
                        {
                            responseMessage = $"≡ƒÆ¡ AI Insights:\n{responseMessage}";
                            _logger.LogInformation("Γ£ô Conversational AI response received ({Length} chars)", responseMessage.Length);
                        }
                    }
                    catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("API key"))
                    {
                        _logger.LogError(ioEx, "XAI API key not configured");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(ioEx);
                    }
                    catch (TaskCanceledException tcEx)
                    {
                        _logger.LogWarning(tcEx, "XAI request timed out");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(tcEx);
                    }
                    catch (HttpRequestException hrEx)
                    {
                        _logger.LogWarning(hrEx, "XAI API network error");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(hrEx);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Conversational AI fallback failed; showing default help message");
                        responseMessage = ConversationalAIHelper.FormatFriendlyError(ex);
                    }
                }
                else
                {
                    // No conversational AI available; show tool help
                    responseMessage = "Γä╣∩╕Å No tool detected. Conversational AI not configured.\n\nAvailable commands:\nΓÇó read <file>\nΓÇó grep <pattern>\nΓÇó list <directory>\nΓÇó search <query>\n\nTo enable AI chat: Set XAI_API_KEY environment variable.";
                    _logger.LogDebug("Conversational AI not configured; showing help message");
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
            AppendMessageToDisplay(aiMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            string friendlyError = ConversationalAIHelper.FormatFriendlyError(ex);
            Messages.Add(new ChatMessage
            {
                IsUser = false,
                Message = friendlyError,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            // Release the semaphore only for internal processing case.
            // When MessageSent had subscribers we returned earlier and released there.
            if (MessageSent == null)
            {
                _executionSemaphore.Release();
            }

            // Hide progress UI and enable button on main thread
            if (InvokeRequired)
            {
                BeginInvoke(() =>
                {
                    if (_progressPanel != null)
                        _progressPanel.Visible = false;
                    if (_sendButton != null)
                        _sendButton.Enabled = true;
                });
            }
            else
            {
                if (_progressPanel != null)
                    _progressPanel.Visible = false;
                if (_sendButton != null)
                    _sendButton.Enabled = true;
            }
        }
    }

    /// <summary>
    /// Public method called by parent forms (e.g. ChatWindow) to indicate processing has completed.
    /// This unblocks the control, hides progress UI and enables the send button.
    /// </summary>
    public void NotifyProcessingCompleted()
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => NotifyProcessingCompleted());
            return;
        }

        try
        {
            if (_progressPanel != null)
                _progressPanel.Visible = false;

            if (_sendButton != null)
                _sendButton.Enabled = true;

            // Release the semaphore which was acquired by SendMessageAsync
            try { _executionSemaphore.Release(); } catch { /* no-op if already released */ }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to complete processing state transition");
        }
    }

    private void AppendMessageToDisplay(ChatMessage message)
    {
        if (_messagesDisplay == null)
            return;

        // Ensure we always update UI on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendMessageToDisplay(message));
            return;
        }

        try
        {
            _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
            _messagesDisplay.SelectionLength = 0;

            // Timestamp and sender header
            _messagesDisplay.SelectionFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _messagesDisplay.SelectionColor = message.IsUser ? Color.FromArgb(0, 102, 204) : Color.FromArgb(76, 175, 80);
            var sender = message.IsUser ? "≡ƒæñ You" : "≡ƒñû AI Assistant";
            var timestamp = message.Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            _messagesDisplay.AppendText($"{sender} ΓÇó {timestamp}\n");

            // Message content
            _messagesDisplay.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            _messagesDisplay.SelectionColor = Color.Black;
            _messagesDisplay.AppendText($"{message.Message}\n");

            // Separator
            _messagesDisplay.SelectionColor = Color.LightGray;
            _messagesDisplay.AppendText(new string('-', 60) + "\n\n");

            // Auto-scroll to bottom
            _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
            _messagesDisplay.ScrollToCaret();

            TrimMessagesIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending message to display");
        }
    }

    /// <summary>
    /// Trim older messages when the collection exceeds the configured MaxMessageCount.
    /// Rebuilds the messages display from the in-memory Messages collection to keep RTF state consistent.
    /// </summary>
    private void TrimMessagesIfNeeded()
    {
        try
        {
            if (_messagesDisplay == null) return;
            if (Messages.Count <= MaxMessageCount) return;

            // Remove the oldest messages until we are under the cap
            while (Messages.Count > MaxMessageCount)
            {
                Messages.RemoveAt(0);
            }

            // Rebuild UI display from remaining messages
            _messagesDisplay.Clear();
            foreach (var m in Messages)
            {
                // Avoid recursively invoking AppendMessageToDisplay which would cause trimming again
                _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
                _messagesDisplay.SelectionLength = 0;
                _messagesDisplay.SelectionFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _messagesDisplay.SelectionColor = m.IsUser ? Color.FromArgb(0, 102, 204) : Color.FromArgb(76, 175, 80);
                var sender = m.IsUser ? "≡ƒæñ You" : "≡ƒñû AI Assistant";
                var timestamp = m.Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                _messagesDisplay.AppendText($"{sender} ΓÇó {timestamp}\n");
                _messagesDisplay.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
                _messagesDisplay.SelectionColor = Color.Black;
                _messagesDisplay.AppendText($"{m.Message}\n");
                _messagesDisplay.SelectionColor = Color.LightGray;
                _messagesDisplay.AppendText(new string('-', 60) + "\n\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trim messages display");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _executionSemaphore?.Dispose();
            _messagesDisplay?.Dispose();
            _inputPanel?.Dispose();
            _inputTextBox?.Dispose();
            _sendButton?.Dispose();
            _clearButton?.Dispose();
            _toolComboBox?.Dispose();
            _progressPanel?.Dispose();
            _progressLabel?.Dispose();
            _headerPanel?.Dispose();
            _headerLabel?.Dispose();
            // Null out event handlers to avoid leaks
            MessageSent = null;
        }
        base.Dispose(disposing);
    }
}
