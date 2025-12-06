using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// AI Chat control for xAI tool execution and conversational interface.
/// Uses Syncfusion SfDataGrid for message display following AccountsForm patterns.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AIChatControl : UserControl
{
    private readonly IAIAssistantService _aiService;
    private readonly ILogger<AIChatControl> _logger;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);

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

    public AIChatControl(IAIAssistantService aiService, ILogger<AIChatControl> logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Messages = new ObservableCollection<ChatMessage>();

        InitializeComponent();
        _logger.LogInformation("AIChatControl initialized successfully");
    }

    private void InitializeComponent()
    {
        SuspendLayout();

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
            Text = "🤖 AI Assistant",
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
            Text = "⏳ Executing tool...",
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
        _toolComboBox.Items.Add("🔍 Auto-detect tool");
        _toolComboBox.Items.AddRange(_aiService.GetAvailableTools().Select(t => $"🛠️ {t.Name} - {t.Description}").ToArray());
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
        _inputTextBox.KeyDown += InputTextBox_KeyDown;

        // Send button with improved styling
        _sendButton = new Button
        {
            Location = new Point(355, 45),
            Width = 70,
            Height = 55,
            Text = "📤\nSend",
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Enabled = true,
            Cursor = Cursors.Hand
        };
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += async (s, e) => await SendMessageAsync();

        _inputPanel.Controls.Add(_toolComboBox);
        _inputPanel.Controls.Add(_inputTextBox);
        _inputPanel.Controls.Add(_sendButton);

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

        // Show progress
        if (_progressPanel != null)
            _progressPanel.Visible = true;

        if (_sendButton != null)
            _sendButton.Enabled = false;

        await _executionSemaphore.WaitAsync();
        try
        {
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
                    responseMessage = $"❌ Error: {result.ErrorMessage}";
                }
                else
                {
                    // Truncate long responses for display
                    var content = result.Content.Length > 1000
                        ? result.Content[..1000] + "\n\n... (truncated for display)"
                        : result.Content;
                    responseMessage = $"✅ Tool: {toolCall.Name}\n{new string('─', 40)}\n{content}";
                }
            }
            else
            {
                responseMessage = "ℹ️ No tool detected.\n\nAvailable commands:\n• read <file>\n• grep <pattern>\n• list <directory>\n• search <query>";
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
            Messages.Add(new ChatMessage
            {
                IsUser = false,
                Message = $"❌ Error: {ex.Message}",
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            _executionSemaphore.Release();

            if (_progressPanel != null)
                _progressPanel.Visible = false;

            if (_sendButton != null)
                _sendButton.Enabled = true;
        }
    }

    private void AppendMessageToDisplay(ChatMessage message)
    {
        if (_messagesDisplay == null)
            return;

        try
        {
            _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
            _messagesDisplay.SelectionLength = 0;

            // Timestamp and sender header
            _messagesDisplay.SelectionFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _messagesDisplay.SelectionColor = message.IsUser ? Color.FromArgb(0, 102, 204) : Color.FromArgb(76, 175, 80);
            var sender = message.IsUser ? "👤 You" : "🤖 AI Assistant";
            var timestamp = message.Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            _messagesDisplay.AppendText($"{sender} • {timestamp}\n");

            // Message content
            _messagesDisplay.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            _messagesDisplay.SelectionColor = Color.Black;
            _messagesDisplay.AppendText($"{message.Message}\n");

            // Separator
            _messagesDisplay.SelectionColor = Color.LightGray;
            _messagesDisplay.AppendText(new string('─', 60) + "\n\n");

            // Auto-scroll to bottom
            _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
            _messagesDisplay.ScrollToCaret();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending message to display");
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
        }
        base.Dispose(disposing);
    }
}
