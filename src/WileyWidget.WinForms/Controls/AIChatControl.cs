using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.Theming;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// AI Chat control for tool execution and conversational interface following MVVM pattern.
/// Provides a professionally designed chat UI with tool detection/execution and optional conversational fallback.
///
/// Integration notes:
/// - DI: Register `AIChatControl`, `AIChatViewModel`, `IAIAssistantService`, and related services.
/// - MVVM: Uses AIChatViewModel for business logic and data binding.
/// - Tool execution: Parses commands (read, grep, search, list) and invokes the IAIAssistantService bridge.
/// - Fallback: If no tool is detected and an `IAIService` is available, uses conversational API responses.
/// - UI: Professional layout with header, messages display, input panel, tool selector, and progress overlay.
/// - Theming: Syncfusion Office2019Colorful theme applied via SkinManager.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public partial class AIChatControl : UserControl
{
    #region Fields

    private readonly AIChatViewModel _viewModel;
    private readonly ILogger<AIChatControl> _logger;

    #endregion

    /// <summary>
    /// Raised when a user message is sent from the chat input.
    /// </summary>
    public event EventHandler<string>? MessageSent;

    /// <summary>
    /// Exposes the underlying message collection for consumers that need read access.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages => _viewModel.Messages;

    #region UI Controls

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
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private ToolStripStatusLabel? _messageCountLabel;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor with ViewModel and logger injection following MVVM and DI patterns.
    /// </summary>
    /// <param name="viewModel">AIChatViewModel instance from DI container</param>
    /// <param name="logger">Logger instance from DI container</param>
    public AIChatControl(
        AIChatViewModel viewModel,
        ILogger<AIChatControl> logger)
    {
        // Validate STA thread requirement for WinForms controls
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "AIChatControl must be created on an STA thread. " +
                "Ensure the application entry point is marked with [STAThread] attribute.");
        }

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();
        SetupDataBindings();

        _logger.LogInformation("AIChatControl initialized successfully with ViewModel");
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes all UI controls with professional layout and Syncfusion theming.
    /// </summary>
    private void InitializeComponent()
    {
        SuspendLayout();

        // Respect system DPI scaling for consistent rendering on high-DPI displays
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(500, 700);
        MinimumSize = new Size(400, 500);

        // === Status Strip (Bottom) ===
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            Font = new Font("Segoe UI", 9f),
            SizingGrip = false
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _messageCountLabel = new ToolStripStatusLabel
        {
            Text = "Messages: 0",
            TextAlign = ContentAlignment.MiddleRight
        };

        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _messageCountLabel });

        // === Header Panel ===
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(15, 5, 15, 5)
        };

        _headerLabel = new Label
        {
            Text = "AI Assistant Chat",
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 12, 0, 0)
        };

        _clearButton = new Button
        {
            Text = "Clear Chat",
            Dock = DockStyle.Right,
            Width = 90,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(5),
            Cursor = Cursors.Hand,
            AccessibleName = "Clear chat button",
            AccessibleDescription = "Clears all messages from the chat history"
        };
        _clearButton.FlatAppearance.BorderSize = 1;
        _clearButton.Click += async (s, e) => await OnClearClickedAsync();

        _headerPanel.Controls.Add(_headerLabel);
        _headerPanel.Controls.Add(_clearButton);

        // === Messages Display (RichTextBox for rich formatting) ===
        _messagesDisplay = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(12),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
            DetectUrls = true,
            AccessibleName = "Chat message history",
            AccessibleDescription = "Displays the conversation history between you and the AI assistant"
        };
        _messagesDisplay.LinkClicked += (s, e) =>
        {
            if (e.LinkText != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to open link: {Link}", e.LinkText);
                }
            }
        };

        // === Progress Panel (Overlay) ===
        _progressPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Visible = false,
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _progressLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Processing...",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.OrangeRed,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
            AccessibleName = "Progress indicator"
        };
        _progressPanel.Controls.Add(_progressLabel);

        // === Input Panel (Bottom above status strip) ===
        _inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 130,
            Padding = new Padding(15, 10, 15, 10)
        };

        // Tool selector dropdown
        _toolComboBox = new ComboBox
        {
            Location = new Point(15, 10),
            Width = 250,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            AccessibleName = "Tool selector",
            AccessibleDescription = "Select a specific tool or use auto-detect mode",
            TabIndex = 1
        };

        // Multi-line input text box
        _inputTextBox = new TextBox
        {
            Location = new Point(15, 45),
            Width = 380,
            Height = 65,
            Multiline = true,
            PlaceholderText = "Type your message here... (Enter to send, Shift+Enter for new line)",
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f),
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = false,
            AccessibleName = "Message input box",
            AccessibleDescription = "Type your message to the AI assistant here",
            TabIndex = 2
        };
        _inputTextBox.KeyDown += InputTextBox_KeyDown;

        // Send button
        _sendButton = new Button
        {
            Location = new Point(405, 45),
            Width = 65,
            Height = 65,
            Text = "Send",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Enabled = true,
            Cursor = Cursors.Hand,
            AccessibleName = "Send message button",
            AccessibleDescription = "Send your message to the AI assistant",
            TabIndex = 3
        };
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += async (s, e) => await OnSendClickedAsync();

        _inputPanel.Controls.AddRange(new Control[] { _toolComboBox, _inputTextBox, _sendButton });

        // === Layout Assembly (proper Z-order from back to front) ===
        Controls.Add(_messagesDisplay);
        Controls.Add(_progressPanel);
        Controls.Add(_inputPanel);
        Controls.Add(_headerPanel);
        Controls.Add(_statusStrip);

        // Tab order for keyboard navigation
        _messagesDisplay.TabIndex = 0;
        _toolComboBox.TabIndex = 1;
        _inputTextBox.TabIndex = 2;
        _sendButton.TabIndex = 3;

        ResumeLayout(false);
        PerformLayout();

        _logger.LogDebug("UI controls initialized successfully");
    }

    /// <summary>
    /// Sets up data bindings between UI controls and ViewModel properties.
    /// </summary>
    private void SetupDataBindings()
    {
        try
        {
            // Bind tool selector to available tools
            if (_toolComboBox != null)
            {
                _toolComboBox.DataSource = _viewModel.AvailableTools;
                _toolComboBox.SelectedItem = _viewModel.SelectedTool;
                _toolComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _viewModel.SelectedTool = _toolComboBox.SelectedItem?.ToString();
                };
            }

            // Bind input text box
            if (_inputTextBox != null)
            {
                _inputTextBox.DataBindings.Add(
                    nameof(_inputTextBox.Text),
                    _viewModel,
                    nameof(_viewModel.InputText),
                    false,
                    DataSourceUpdateMode.OnPropertyChanged);
            }

            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Bind message collection
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;

            _logger.LogDebug("Data bindings established successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up data bindings");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles ViewModel property changes to update UI.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ViewModel_PropertyChanged(sender, e));
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(_viewModel.IsLoading):
                    UpdateLoadingState();
                    break;

                case nameof(_viewModel.StatusText):
                    UpdateStatusText();
                    break;

                case nameof(_viewModel.ErrorMessage):
                    UpdateErrorDisplay();
                    break;

                case nameof(_viewModel.MessageCount):
                    UpdateMessageCount();
                    break;

                case nameof(_viewModel.CurrentPersonality):
                    UpdatePersonalityDisplay();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling property change: {PropertyName}", e.PropertyName);
        }
    }

    /// <summary>
    /// Handles collection changes in Messages to update the display.
    /// </summary>
    private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Messages_CollectionChanged(sender, e));
            return;
        }

        try
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (ChatMessage message in e.NewItems)
                {
                    AppendMessageToDisplay(message);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                _messagesDisplay?.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message collection change");
        }
    }

    /// <summary>
    /// Handles keyboard input in the message text box.
    /// </summary>
    /// <summary>
    /// Handles keyboard input in the message text box.
    /// </summary>
    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = OnSendClickedAsync();
        }
    }

    /// <summary>
    /// Handles send button click.
    /// </summary>
    private async Task OnSendClickedAsync()
    {
        try
        {
            if (_viewModel.SendMessageCommand.CanExecute(null))
            {
                await _viewModel.SendMessageCommand.ExecuteAsync(null);

                var latestUserMessage = _viewModel.Messages.LastOrDefault(m => m.IsUser);
                if (latestUserMessage != null)
                {
                    MessageSent?.Invoke(this, latestUserMessage.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing send command");
            MessageBox.Show(
                "Failed to send message. Please try again.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles clear button click.
    /// </summary>
    private async Task OnClearClickedAsync()
    {
        try
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all messages?",
                "Confirm Clear",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _viewModel.ClearMessagesCommand.Execute(null);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing clear command");
        }
    }

    #endregion

    #region UI Update Methods

    /// <summary>
    /// Updates the loading state UI elements.
    /// </summary>
    private void UpdateLoadingState()
    {
        try
        {
            if (_progressPanel != null)
            {
                _progressPanel.Visible = _viewModel.IsLoading;
            }

            if (_sendButton != null)
            {
                _sendButton.Enabled = !_viewModel.IsLoading;
            }

            if (_inputTextBox != null)
            {
                _inputTextBox.Enabled = !_viewModel.IsLoading;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loading state");
        }
    }

    /// <summary>
    /// Updates the status text display.
    /// </summary>
    private void UpdateStatusText()
    {
        try
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = _viewModel.StatusText;
            }

            if (_progressLabel != null)
            {
                _progressLabel.Text = _viewModel.StatusText;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status text");
        }
    }

    /// <summary>
    /// Updates the error message display.
    /// </summary>
    private void UpdateErrorDisplay()
    {
        try
        {
            if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                if (_statusLabel != null)
                {
                    _statusLabel.ForeColor = Color.Red;
                    _statusLabel.Text = _viewModel.ErrorMessage;
                }
            }
            else
            {
                if (_statusLabel != null)
                {
                    _statusLabel.ForeColor = SystemColors.ControlText;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating error display");
        }
    }

    /// <summary>
    /// Updates the message count display.
    /// </summary>
    private void UpdateMessageCount()
    {
        try
        {
            if (_messageCountLabel != null)
            {
                _messageCountLabel.Text = $"Messages: {_viewModel.MessageCount}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating message count");
        }
    }

    /// <summary>
    /// Updates the personality display in the header.
    /// </summary>
    private void UpdatePersonalityDisplay()
    {
        try
        {
            if (_headerLabel != null && _viewModel.HasConversationalAI)
            {
                _headerLabel.Text = $"AI Assistant ({_viewModel.CurrentPersonality})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating personality display");
        }
    }

    /// <summary>
    /// Allows external consumers to signal that processing is complete so UI overlays are hidden.
    /// </summary>
    public void NotifyProcessingCompleted()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(NotifyProcessingCompleted));
            return;
        }

        _viewModel.IsLoading = false;
        _viewModel.StatusText = "Ready";
        UpdateLoadingState();
    }

    /// <summary>
    /// Appends a chat message to the RichTextBox display with formatting.
    /// </summary>
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
            _messagesDisplay.SelectionColor = message.IsUser ? Color.DodgerBlue : Color.SeaGreen;
            var sender = message.IsUser ? "You" : "AI Assistant";
            var timestamp = message.Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            _messagesDisplay.AppendText($"{sender} - {timestamp}\n");

            // Message content
            _messagesDisplay.SelectionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            _messagesDisplay.SelectionColor = SystemColors.ControlText;
            _messagesDisplay.AppendText($"{message.Message}\n");

            // Separator
            _messagesDisplay.SelectionColor = Color.LightGray;
            _messagesDisplay.AppendText(new string('-', 80) + "\n\n");

            // Auto-scroll to bottom
            _messagesDisplay.SelectionStart = _messagesDisplay.TextLength;
            _messagesDisplay.ScrollToCaret();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending message to display");
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Handles control load event.
    /// </summary>
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            // Show welcome message on first load
            await _viewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during control load");
        }
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
                _viewModel.Dispose();
            }

            // Dispose controls
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
            _statusStrip?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion
}
