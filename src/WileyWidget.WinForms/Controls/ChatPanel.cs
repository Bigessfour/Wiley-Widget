using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.Controls;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using Syncfusion.WinForms.Input;
using Syncfusion.Windows.Forms.Tools;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.ViewModels;

namespace WileyWidget.WinForms.Controls;

/// <summary>
/// ChatPanel provides comprehensive AI chat interaction with conversation management.
/// Features message history, conversation persistence, context-aware responses, and activity logging.
/// Implements modern MVVM architecture with Syncfusion WinForms controls.
/// </summary>
[SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
public sealed class ChatPanel : UserControl
{
    #region Fields

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatPanel> _logger;
    private ChatPanelViewModel? _viewModel;

    // UI Controls
    private TableLayoutPanel? _mainLayout;
    private Panel? _headerPanel;
    private Label? _titleLabel;
    private SfButton? _closeButton;
    private Panel? _toolbarPanel;
    private SfButton? _newConversationButton;
    private SfButton? _saveButton;
    private SfButton? _exportButton;
    private SfButton? _toggleSidebarButton;
    private SplitContainer? _mainSplitContainer;
    private Panel? _conversationsPanel;
    private SfDataGrid? _conversationsGrid;
    private Panel? _chatPanel;
    private SplitContainer? _chatSplitContainer;
    private Panel? _messagesPanel;
    private FlowLayoutPanel? _messagesFlowPanel;
    private Panel? _inputPanel;
    private TextBox? _inputTextBox;
    private SfButton? _sendButton;
    private Panel? _summaryPanel;
    private Label? _messageCountLabel;
    private Label? _conversationCountLabel;
    private Label? _contextLabel;
    private StatusStrip? _statusStrip;
    private ToolStripStatusLabel? _statusLabel;
    private Panel? _loadingOverlay;
    private Label? _loadingLabel;
    private Panel? _noDataOverlay;
    private Label? _noDataLabel;

    #endregion

    #region Constructor
    /// <summary>
    /// Constructor accepting service provider for DI resolution.
    /// Initializes the ChatPanel with required services and ViewModel.
    /// </summary>
    public ChatPanel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ChatPanel>>(serviceProvider);

        try
        {
            InitializeComponent();
            InitializeViewModel();
            _logger.LogInformation("ChatPanel created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ChatPanel");
            throw new InvalidOperationException("Failed to initialize ChatPanel", ex);
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the ViewModel with required services from DI.
    /// </summary>
    private void InitializeViewModel()
    {
        try
        {
            var aiService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IAIService>(_serviceProvider);
            var conversationRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IConversationRepository>(_serviceProvider);
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<ChatPanelViewModel>>(_serviceProvider);
            var contextExtractionService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIContextExtractionService>(_serviceProvider);
            var activityLogRepository = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IActivityLogRepository>(_serviceProvider);

            _viewModel = new ChatPanelViewModel(
                aiService,
                conversationRepository,
                logger,
                contextExtractionService,
                activityLogRepository);

            _logger.LogInformation("ChatPanelViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ChatPanelViewModel");
            throw;
        }
    }

    /// <summary>
    /// Initialize all UI controls and layout.
    /// </summary>
    private void InitializeComponent()
    {
        SuspendLayout();

        // Panel properties
        Name = "ChatPanel";
        Dock = DockStyle.Fill;
        Padding = new Padding(0);

        // Main layout
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Name = "MainLayout"
        };
        _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Header
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Toolbar
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Main content
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Summary
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Status

        // Create sections
        CreateHeaderSection();
        CreateToolbarSection();
        CreateMainContentSection();
        CreateSummarySection();
        CreateStatusSection();
        CreateOverlays();

        // Add main layout to panel
        Controls.Add(_mainLayout);

        // Add overlays on top
        if (_loadingOverlay != null) Controls.Add(_loadingOverlay);
        if (_noDataOverlay != null) Controls.Add(_noDataOverlay);

        ResumeLayout(false);
        PerformLayout();

        _logger.LogInformation("ChatPanel UI initialized");
    }

    /// <summary>
    /// Create the header section with title and close button.
    /// </summary>
    private void CreateHeaderSection()
    {
        _headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 5, 10, 5),
            Name = "HeaderPanel"
        };

        _titleLabel = new Label
        {
            Text = "💬 AI Chat",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Name = "TitleLabel"
        };

        _closeButton = new SfButton
        {
            Text = "✖",
            Size = new Size(40, 35),
            Dock = DockStyle.Right,
            Font = new Font("Segoe UI", 12F),
            Name = "CloseButton",
            AccessibleName = "Close Chat Panel"
        };
        _closeButton.Click += CloseButton_Click;

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_closeButton);

        _mainLayout?.Controls.Add(_headerPanel, 0, 0);
    }

    /// <summary>
    /// Create the toolbar section with search, actions, and filters.
    /// </summary>
    private void CreateToolbarSection()
    {
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(5),
            Name = "ToolbarPanel"
        };

        var toolbarFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(5)
        };

        _newConversationButton = new SfButton
        {
            Text = "➕ New",
            Size = new Size(80, 30),
            Name = "NewButton"
        };

        _saveButton = new SfButton
        {
            Text = "💾 Save",
            Size = new Size(80, 30),
            Name = "SaveButton"
        };

        _exportButton = new SfButton
        {
            Text = "📤 Export",
            Size = new Size(90, 30),
            Name = "ExportButton"
        };

        _toggleSidebarButton = new SfButton
        {
            Text = "📋 History",
            Size = new Size(95, 30),
            Name = "ToggleSidebarButton"
        };

        toolbarFlow.Controls.Add(_newConversationButton);
        toolbarFlow.Controls.Add(_saveButton);
        toolbarFlow.Controls.Add(_exportButton);
        toolbarFlow.Controls.Add(_toggleSidebarButton);

        _toolbarPanel.Controls.Add(toolbarFlow);
        _mainLayout?.Controls.Add(_toolbarPanel, 0, 1);
    }

    /// <summary>
    /// Create the main content section with split container for conversations and chat.
    /// </summary>
    private void CreateMainContentSection()
    {
        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 300,
            BorderStyle = BorderStyle.FixedSingle,
            Name = "MainSplitContainer"
        };

        // Left panel: Conversations list
        CreateConversationsPanel();
        _mainSplitContainer.Panel1.Controls.Add(_conversationsPanel);

        // Right panel: Chat interface
        CreateChatPanel();
        _mainSplitContainer.Panel2.Controls.Add(_chatPanel);

        _mainLayout?.Controls.Add(_mainSplitContainer, 0, 2);
    }

    /// <summary>
    /// Create the conversations list panel with grid.
    /// </summary>
    private void CreateConversationsPanel()
    {
        _conversationsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Name = "ConversationsPanel"
        };

        var headerLabel = new Label
        {
            Text = "Conversation History",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5)
        };

        _conversationsGrid = new SfDataGrid
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowEditing = false,
            AllowSorting = true,
            AllowFiltering = true,
            SelectionMode = GridSelectionMode.Single,
            Name = "ConversationsGrid",
            AccessibleName = "Conversations Grid"
        };

        // Configure columns
        _conversationsGrid.Columns.Add(new GridTextColumn
        {
            MappingName = nameof(ConversationHistory.Title),
            HeaderText = "Title",
            Width = 180
        });
        _conversationsGrid.Columns.Add(new GridNumericColumn
        {
            MappingName = nameof(ConversationHistory.MessageCount),
            HeaderText = "Messages",
            Width = 70,
            Format = "N0"
        });
        _conversationsGrid.Columns.Add(new GridDateTimeColumn
        {
            MappingName = nameof(ConversationHistory.UpdatedAt),
            HeaderText = "Updated",
            Width = 120,
            Format = "g"
        });

        _conversationsPanel.Controls.Add(_conversationsGrid);
        _conversationsPanel.Controls.Add(headerLabel);
    }

    /// <summary>
    /// Create the chat panel with messages and input.
    /// </summary>
    private void CreateChatPanel()
    {
        _chatPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Name = "ChatPanel"
        };

        _chatSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400,
            FixedPanel = FixedPanel.Panel2,
            IsSplitterFixed = true,
            Name = "ChatSplitContainer"
        };

        // Top: Messages display
        CreateMessagesPanel();
        _chatSplitContainer.Panel1.Controls.Add(_messagesPanel);

        // Bottom: Input area
        CreateInputPanel();
        _chatSplitContainer.Panel2.Controls.Add(_inputPanel);

        _chatPanel.Controls.Add(_chatSplitContainer);
    }

    /// <summary>
    /// Create the messages display panel.
    /// </summary>
    private void CreateMessagesPanel()
    {
        _messagesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Name = "MessagesPanel"
        };

        _messagesFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(5),
            Name = "MessagesFlowPanel"
        };

        _messagesPanel.Controls.Add(_messagesFlowPanel);
    }

    /// <summary>
    /// Create the input panel with text box and send button.
    /// </summary>
    private void CreateInputPanel()
    {
        _inputPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Height = 80,
            Name = "InputPanel"
        };

        _inputTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Name = "InputTextBox"
        };

        _sendButton = new SfButton
        {
            Text = "📤 Send",
            Size = new Size(100, 60),
            Dock = DockStyle.Right,
            Name = "SendButton"
        };

        _inputPanel.Controls.Add(_inputTextBox);
        _inputPanel.Controls.Add(_sendButton);
    }

    /// <summary>
    /// Create the summary section with statistics.
    /// </summary>
    private void CreateSummarySection()
    {
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Name = "SummaryPanel"
        };

        var summaryFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false
        };

        _messageCountLabel = new Label
        {
            Text = "Messages: 0",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(5),
            Name = "MessageCountLabel"
        };

        _conversationCountLabel = new Label
        {
            Text = "Conversations: 0",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(5),
            Name = "ConversationCountLabel"
        };

        _contextLabel = new Label
        {
            Text = "",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            Padding = new Padding(5),
            Name = "ContextLabel"
        };

        summaryFlow.Controls.Add(_messageCountLabel);
        summaryFlow.Controls.Add(new Label { Text = "|", Padding = new Padding(5) });
        summaryFlow.Controls.Add(_conversationCountLabel);
        summaryFlow.Controls.Add(_contextLabel);

        _summaryPanel.Controls.Add(summaryFlow);
        _mainLayout?.Controls.Add(_summaryPanel, 0, 3);
    }

    /// <summary>
    /// Create the status strip section.
    /// </summary>
    private void CreateStatusSection()
    {
        _statusStrip = new StatusStrip
        {
            Dock = DockStyle.Fill,
            Name = "StatusStrip"
        };

        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Name = "StatusLabel"
        };

        _statusStrip.Items.Add(_statusLabel);
        _mainLayout?.Controls.Add(_statusStrip, 0, 4);
    }

    /// <summary>
    /// Create loading and no-data overlays.
    /// </summary>
    private void CreateOverlays()
    {
        // Loading overlay
        _loadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            // BackColor removed - let SkinManager handle theming
            Visible = false,
            Name = "LoadingOverlay"
        };

        _loadingLabel = new Label
        {
            Text = "⏳ Loading...",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            // BackColor removed - let SkinManager handle theming
            Name = "LoadingLabel"
        };
        _loadingLabel.Location = new Point(
            (_loadingOverlay.Width - _loadingLabel.Width) / 2,
            (_loadingOverlay.Height - _loadingLabel.Height) / 2);
        _loadingOverlay.Controls.Add(_loadingLabel);

        // No data overlay
        _noDataOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            // BackColor removed - let SkinManager handle theming
            Visible = false,
            Name = "NoDataOverlay"
        };

        _noDataLabel = new Label
        {
            Text = "📭 No conversations yet.\nClick 'New' to start chatting!",
            Font = new Font("Segoe UI", 14F),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            // BackColor removed - let SkinManager handle theming
            Name = "NoDataLabel"
        };
        _noDataLabel.Location = new Point(
            (_noDataOverlay.Width - _noDataLabel.Width) / 2,
            (_noDataOverlay.Height - _noDataLabel.Height) / 2);
        _noDataOverlay.Controls.Add(_noDataLabel);
    }

    #endregion

    #region Data Binding

    /// <summary>
    /// Set up all data bindings between UI and ViewModel.
    /// </summary>
    private void SetupBindings()
    {
        if (_viewModel == null) return;

        try
        {
            // Bind collections
            if (_conversationsGrid != null)
            {
                _conversationsGrid.DataSource = _viewModel.FilteredConversations;
            }

            // Two-way bind input text
            if (_inputTextBox != null)
            {
                _inputTextBox.DataBindings.Add(nameof(_inputTextBox.Text), _viewModel, nameof(_viewModel.InputText), false, DataSourceUpdateMode.OnPropertyChanged);
            }

            // Bind to property changed events
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Bind commands to buttons
            WireCommands();

            // Initial update
            UpdateUIFromViewModel();

            _logger.LogInformation("Data bindings established successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up data bindings");
        }
    }

    /// <summary>
    /// Wire up command handlers to UI buttons.
    /// </summary>
    private void WireCommands()
    {
        if (_viewModel == null) return;

        if (_sendButton != null)
        {
            _sendButton.Click += async (s, e) =>
            {
                if (_viewModel.SendMessageCommand.CanExecute(null))
                {
                    await _viewModel.SendMessageCommand.ExecuteAsync(null);
                    RefreshMessages();
                }
            };
        }

        if (_newConversationButton != null)
        {
            _newConversationButton.Click += (s, e) =>
            {
                _viewModel.NewConversationCommand.Execute(null);
                RefreshMessages();
            };
        }

        if (_saveButton != null)
        {
            _saveButton.Click += async (s, e) => await _viewModel.SaveConversationCommand.ExecuteAsync(null);
        }

        if (_exportButton != null)
        {
            _exportButton.Click += async (s, e) => await _viewModel.ExportConversationCommand.ExecuteAsync(null);
        }

        if (_toggleSidebarButton != null)
        {
            _toggleSidebarButton.Click += (s, e) => _viewModel.ToggleConversationsListCommand.Execute(null);
        }

        // Grid selection change
        if (_conversationsGrid != null)
        {
            _conversationsGrid.SelectionChanged += (s, e) =>
            {
                if (_conversationsGrid.SelectedItem is ConversationHistory conv)
                {
                    _viewModel.SelectedConversation = conv;
                }
            };
        }

        // Enter key to send message
        if (_inputTextBox != null)
        {
            _inputTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && e.Control && _viewModel.SendMessageCommand.CanExecute(null))
                {
                    e.SuppressKeyPress = true;
                    _sendButton?.PerformClick();
                }
            };
        }
    }

    /// <summary>
    /// Handle ViewModel property changes and update UI accordingly.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            try
            {
                Invoke(() => ViewModel_PropertyChanged(sender, e));
            }
            catch (ObjectDisposedException)
            {
                // Control disposed during invoke
            }
            return;
        }

        try
        {
            switch (e.PropertyName)
            {
                case nameof(ChatPanelViewModel.IsLoading):
                    UpdateLoadingState();
                    break;
                case nameof(ChatPanelViewModel.StatusText):
                    UpdateStatusText();
                    break;
                case nameof(ChatPanelViewModel.ErrorMessage):
                    UpdateErrorDisplay();
                    break;
                case nameof(ChatPanelViewModel.MessageCount):
                    UpdateMessageCountDisplay();
                    break;
                case nameof(ChatPanelViewModel.ConversationCount):
                    UpdateConversationCountDisplay();
                    break;
                case nameof(ChatPanelViewModel.ContextDescription):
                    UpdateContextDisplay();
                    break;
                case nameof(ChatPanelViewModel.ShowConversationsList):
                    UpdateSidebarVisibility();
                    break;
                case nameof(ChatPanelViewModel.Messages):
                    RefreshMessages();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling property change: {PropertyName}", e.PropertyName);
        }
    }

    /// <summary>
    /// Update all UI elements from current ViewModel state.
    /// </summary>
    private void UpdateUIFromViewModel()
    {
        if (_viewModel == null) return;

        UpdateLoadingState();
        UpdateStatusText();
        UpdateErrorDisplay();
        UpdateMessageCountDisplay();
        UpdateConversationCountDisplay();
        UpdateContextDisplay();
        UpdateSidebarVisibility();
        RefreshMessages();
    }

    #endregion

    #region UI Update Methods

    /// <summary>
    /// Update loading overlay visibility based on IsLoading state.
    /// </summary>
    private void UpdateLoadingState()
    {
        if (_viewModel == null || _loadingOverlay == null) return;

        _loadingOverlay.Visible = _viewModel.IsLoading;
        _loadingOverlay.BringToFront();

        // Disable inputs during loading
        if (_sendButton != null) _sendButton.Enabled = !_viewModel.IsLoading && _viewModel.SendMessageCommand.CanExecute(null);
        if (_inputTextBox != null) _inputTextBox.Enabled = !_viewModel.IsLoading;
    }

    /// <summary>
    /// Update status bar text.
    /// </summary>
    private void UpdateStatusText()
    {
        if (_viewModel == null || _statusLabel == null) return;
        _statusLabel.Text = _viewModel.StatusText ?? "Ready";
    }

    /// <summary>
    /// Update error message display.
    /// </summary>
    private void UpdateErrorDisplay()
    {
        if (_viewModel == null) return;

        if (!string.IsNullOrEmpty(_viewModel.ErrorMessage) && _statusLabel != null)
        {
            _statusLabel.Text = $"❌ {_viewModel.ErrorMessage}";
            _statusLabel.ForeColor = Color.Red; // Semantic error color (allowed exception)
        }
        else if (_statusLabel != null)
        {
            _statusLabel.Text = "";
            // ForeColor reset to default - let SkinManager handle theming
        }
    }

    /// <summary>
    /// Update message count display.
    /// </summary>
    private void UpdateMessageCountDisplay()
    {
        if (_viewModel == null || _messageCountLabel == null) return;
        _messageCountLabel.Text = $"Messages: {_viewModel.MessageCount}";
    }

    /// <summary>
    /// Update conversation count display.
    /// </summary>
    private void UpdateConversationCountDisplay()
    {
        if (_viewModel == null || _conversationCountLabel == null) return;
        _conversationCountLabel.Text = $"Conversations: {_viewModel.ConversationCount}";
    }

    /// <summary>
    /// Update context description display.
    /// </summary>
    private void UpdateContextDisplay()
    {
        if (_viewModel == null || _contextLabel == null) return;

        if (!string.IsNullOrEmpty(_viewModel.ContextDescription))
        {
            _contextLabel.Text = $"📋 Context: {_viewModel.ContextDescription}";
            _contextLabel.Visible = true;
        }
        else
        {
            _contextLabel.Visible = false;
        }
    }

    /// <summary>
    /// Update sidebar visibility.
    /// </summary>
    private void UpdateSidebarVisibility()
    {
        if (_viewModel == null || _mainSplitContainer == null) return;
        _mainSplitContainer.Panel1Collapsed = !_viewModel.ShowConversationsList;
    }

    /// <summary>
    /// Refresh the messages display panel.
    /// </summary>
    private void RefreshMessages()
    {
        if (_viewModel == null || _messagesFlowPanel == null) return;

        try
        {
            _messagesFlowPanel.SuspendLayout();
            _messagesFlowPanel.Controls.Clear();

            foreach (var message in _viewModel.Messages)
            {
                var messageControl = CreateMessageControl(message);
                _messagesFlowPanel.Controls.Add(messageControl);
            }

            _messagesFlowPanel.ResumeLayout();

            // Scroll to bottom
            if (_messagesPanel != null)
            {
                _messagesPanel.ScrollControlIntoView(_messagesFlowPanel);
            }

            // Show/hide no data overlay
            if (_noDataOverlay != null)
            {
                _noDataOverlay.Visible = _viewModel.Messages.Count == 0 && !_viewModel.IsLoading;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh messages display");
        }
    }

    /// <summary>
    /// Create a visual control for a chat message.
    /// </summary>
    private Panel CreateMessageControl(ChatMessage message)
    {
        var messagePanel = new Panel
        {
            Width = _messagesFlowPanel?.Width - 30 ?? 600,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10),
            Margin = new Padding(5),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Message colors removed - let SkinManager handle theming
        // User/AI message distinction handled by other means

        var headerLabel = new Label
        {
            Text = message.IsUser ? "👤 You" : "🤖 AI Assistant",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top
        };

        var messageLabel = new Label
        {
            Text = message.Message,
            Font = new Font("Segoe UI", 9.5F),
            AutoSize = true,
            MaximumSize = new Size(messagePanel.Width - 20, 0),
            Dock = DockStyle.Top,
            Padding = new Padding(0, 5, 0, 0)
        };

        var timestampLabel = new Label
        {
            Text = message.Timestamp.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture),
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 3, 0, 0)
        };

        messagePanel.Controls.Add(timestampLabel);
        messagePanel.Controls.Add(messageLabel);
        messagePanel.Controls.Add(headerLabel);

        return messagePanel;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle panel load event.
    /// </summary>
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (DesignMode) return;

        try
        {
            SetupBindings();

            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }

            _logger.LogInformation("ChatPanel loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ChatPanel load");
            MessageBox.Show(
                $"Failed to load chat panel: {ex.Message}",
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handle close button click.
    /// </summary>
    private void CloseButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var parent = Parent;
            parent?.Controls.Remove(this);
            Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing chat panel");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set conversation context from external source (e.g., selected account, budget period).
    /// Delegates to ViewModel.
    /// </summary>
    /// <param name="contextDescription">Human-readable context description</param>
    /// <param name="contextData">Optional structured context data</param>
    public void SetContext(string contextDescription, Dictionary<string, object>? contextData = null)
    {
        try
        {
            _viewModel?.SetContext(contextDescription, contextData);
            _logger.LogInformation("Context set: {Context}", contextDescription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set context");
        }
    }

    /// <summary>
    /// Clear all conversation messages.
    /// </summary>
    public void ClearConversation()
    {
        try
        {
            _viewModel?.ClearMessagesCommand.Execute(null);
            _logger.LogInformation("Conversation cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing conversation");
        }
    }

    /// <summary>
    /// Load a specific conversation by ID.
    /// </summary>
    public async Task LoadConversationAsync(string conversationId)
    {
        if (_viewModel == null || string.IsNullOrWhiteSpace(conversationId)) return;

        try
        {
            await _viewModel.LoadConversationCommand.ExecuteAsync(conversationId);
            _logger.LogInformation("Loaded conversation: {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation: {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Save the current conversation.
    /// </summary>
    public async Task SaveConversationAsync()
    {
        if (_viewModel == null) return;

        try
        {
            await _viewModel.SaveConversationCommand.ExecuteAsync(null);
            _logger.LogInformation("Saved current conversation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation");
        }
    }

    /// <summary>
    /// Delete a conversation by ID.
    /// </summary>
    public async Task DeleteConversationAsync(string conversationId)
    {
        if (_viewModel == null || string.IsNullOrWhiteSpace(conversationId)) return;

        try
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this conversation? This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                await _viewModel.DeleteConversationCommand.ExecuteAsync(conversationId);
                _logger.LogInformation("Deleted conversation: {ConversationId}", conversationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation: {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Start a new conversation (clears current messages).
    /// </summary>
    public void StartNewConversation()
    {
        try
        {
            _viewModel?.NewConversationCommand.Execute(null);
            RefreshMessages();
            _logger.LogInformation("Started new conversation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting new conversation");
        }
    }

    /// <summary>
    /// Get recent conversations from history.
    /// </summary>
    public async Task<List<ConversationHistory>> GetRecentConversationsAsync(int limit = 20)
    {
        if (_viewModel == null) return new List<ConversationHistory>();

        try
        {
            await _viewModel.LoadConversationsCommand.ExecuteAsync(null);
            return _viewModel.Conversations.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent conversations");
            return new List<ConversationHistory>();
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Clean up panel resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Dispose ViewModel
                _viewModel?.Dispose();

                // Dispose UI controls
                _conversationsGrid?.Dispose();
                _inputTextBox?.Dispose();
                _sendButton?.Dispose();
                _newConversationButton?.Dispose();
                _saveButton?.Dispose();
                _exportButton?.Dispose();
                _toggleSidebarButton?.Dispose();
                _closeButton?.Dispose();
                _mainSplitContainer?.Dispose();
                _chatSplitContainer?.Dispose();
                _statusStrip?.Dispose();
                _loadingOverlay?.Dispose();
                _noDataOverlay?.Dispose();

                _logger.LogInformation("ChatPanel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during ChatPanel disposal");
            }
        }

        base.Dispose(disposing);
    }

    #endregion
}
