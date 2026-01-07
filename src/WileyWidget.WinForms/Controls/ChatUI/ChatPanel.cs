using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;
using Syncfusion.Windows.Forms;
using ThemeColorsAlias = WileyWidget.WinForms.Themes.ThemeColors;
using WileyWidget.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls.ChatUI;

/// <summary>
/// Complete chat panel with message list, input controls, and file attachment support.
/// Based on winforms-chat reference implementation but adapted for WileyWidget architecture.
/// Integrates with ChatPanelViewModel for MVVM pattern and DI.
/// </summary>
public sealed class ChatPanel : UserControl
{
    private ChatMessageList _messageList;
    private TextBoxExt _inputTextBox;
    private SfButton _sendButton;
    private SfButton _attachButton;
    private SfButton _removeButton;
    private GradientPanelExt _topPanel;
    private GradientPanelExt _bottomPanel;
    private Label _titleLabel;
    private Label _statusLabel;
    private Label _contextLabel;
    private readonly OpenFileDialog _fileDialog = new();

    private ChatPanelViewModel? _viewModel;
    private byte[]? _attachment;
    private string? _attachmentName;

    public ChatPanel(ChatPanelViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to Messages collection changes for real-time streaming updates
        _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;

        InitializeUI();
        SyncFromViewModel();
    }
    private void InitializeUI()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;

        // Top panel with title and context (themed header)
        _topPanel = new GradientPanelExt
        {
            Dock = DockStyle.Top,
            Height = 89,
            Padding = new Padding(18, 17, 18, 17),
            BorderStyle = BorderStyle.None
        };

        _titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Text = "AI Chat",
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        _contextLabel = new Label
        {
            Dock = DockStyle.Top,
            Text = string.Empty,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Visible = false,
            BackColor = Color.Transparent
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Right,
            Text = "Ready",
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        };

        _topPanel.Controls.Add(_statusLabel);
        _topPanel.Controls.Add(_contextLabel);
        _topPanel.Controls.Add(_titleLabel);

        // Message list (middle, fills available space)
        _messageList = new ChatMessageList
        {
            Dock = DockStyle.Fill
        };
        SfSkinManager.SetVisualStyle(_messageList, ThemeColorsAlias.DefaultTheme);

        // Bottom panel with input controls (themed footer)
        _bottomPanel = new GradientPanelExt
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            Padding = new Padding(18, 12, 18, 12),
            BorderStyle = BorderStyle.None
        };

        _inputTextBox = new TextBoxExt
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Font = new Font("Segoe UI", 10F),
            PlaceholderText = "Type your message here..."
        };

        _attachButton = new SfButton
        {
            Dock = DockStyle.Right,
            Width = 41,
            Height = 38,
            Text = "📎",
            Font = new Font("Segoe UI", 12F)
        };
        SfSkinManager.SetVisualStyle(_attachButton, ThemeColorsAlias.DefaultTheme);

        _removeButton = new SfButton
        {
            Dock = DockStyle.Right,
            Width = 22,
            Height = 38,
            Text = "X",
            Font = new Font("Segoe UI Symbol", 9.75F, FontStyle.Bold),
            Visible = false
        };
        SfSkinManager.SetVisualStyle(_removeButton, ThemeColorsAlias.DefaultTheme);

        _sendButton = new SfButton
        {
            Dock = DockStyle.Right,
            Width = 88,
            Height = 38,
            Text = "Send",
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold)
        };
        SfSkinManager.SetVisualStyle(_sendButton, ThemeColorsAlias.DefaultTheme);

        _bottomPanel.Controls.Add(_inputTextBox);
        _bottomPanel.Controls.Add(_attachButton);
        _bottomPanel.Controls.Add(_removeButton);
        _bottomPanel.Controls.Add(_sendButton);

        // Add all panels to the control
        Controls.Add(_messageList);
        Controls.Add(_bottomPanel);
        Controls.Add(_topPanel);

        // Wire up events
        _sendButton.Click += SendButton_Click;
        _attachButton.Click += AttachButton_Click;
        _removeButton.Click += RemoveButton_Click;
        _inputTextBox.KeyDown += InputTextBox_KeyDown;
        _inputTextBox.TextChanged += InputTextBox_TextChanged;

        // Configure file dialog
        _fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _fileDialog.Multiselect = false;
        _fileDialog.Title = "Attach File";
        _fileDialog.Filter = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif|Documents (*.pdf;*.docx)|*.pdf;*.docx";
    }

    private void SyncFromViewModel()
    {
        if (_viewModel == null) return;

        // Sync status
        _statusLabel.Text = _viewModel.StatusText;

        // Sync messages
        _messageList.SetItems(_viewModel.Messages);

        // Sync input state
        _inputTextBox.Text = _viewModel.InputText;
        _sendButton.Enabled = _viewModel.CanSendMessage();
        _inputTextBox.Enabled = !_viewModel.IsLoading;
    }

    /// <summary>
    /// Bind this control to a ChatPanelViewModel instance.
    /// </summary>
    public void SetViewModel(ChatPanelViewModel viewModel)
    {
        if (_viewModel != null)
        {
            // Unsubscribe from old view model
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        // Subscribe to view model property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to Messages collection changes for real-time streaming updates
        _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;

        // Initial sync
        SyncFromViewModel();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ViewModel_PropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ChatPanelViewModel.Messages):
                // CRITICAL: Re-subscribe to the new Messages collection when it changes
                // This handles cases where the ViewModel replaces the entire collection
                if (_viewModel != null)
                {
                    _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
                    _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
                }
                SyncMessages();
                break;
            case nameof(ChatPanelViewModel.StatusText):
                _statusLabel.Text = _viewModel?.StatusText ?? "Ready";
                break;
            case nameof(ChatPanelViewModel.ConversationTitle):
                _titleLabel.Text = _viewModel?.ConversationTitle ?? "AI Chat";
                break;
            case nameof(ChatPanelViewModel.ContextDescription):
                var context = _viewModel?.ContextDescription;
                _contextLabel.Text = context ?? string.Empty;
                _contextLabel.Visible = !string.IsNullOrWhiteSpace(context);
                break;
            case nameof(ChatPanelViewModel.IsLoading):
                var isLoading = _viewModel?.IsLoading ?? false;
                _sendButton.Enabled = !isLoading && _viewModel?.CanSendMessage() == true;
                _inputTextBox.Enabled = !isLoading;
                _statusLabel.Text = isLoading ? "Processing..." : (_viewModel?.StatusText ?? "Ready");
                break;
            case nameof(ChatPanelViewModel.InputText):
                _inputTextBox.Text = _viewModel?.InputText ?? string.Empty;
                _sendButton.Enabled = _viewModel?.CanSendMessage() == true;
                break;
        }
    }

    private void SyncMessages()
    {
        if (_viewModel == null)
            return;

        try
        {
            var messages = _viewModel.Messages.ToList();
            _messageList.SetItems(messages);
            // SetItems already calls ScrollToBottom internally, no need to call it again
        }
        catch (Exception ex)
        {
            // Log but don't crash the UI
            System.Diagnostics.Debug.WriteLine($"Error syncing messages: {ex.Message}");
        }
    }

    private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Messages_CollectionChanged(sender, e));
            return;
        }

        // Handle real-time message updates for streaming
        // ALWAYS sync messages regardless of action type for simplicity and reliability
        SyncMessages();
    }

    private async void SendButton_Click(object? sender, EventArgs e)
    {
        if (_viewModel == null)
            return;

        var message = _inputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(message) && _attachment == null)
            return;

        try
        {
            // Handle text message
            if (!string.IsNullOrWhiteSpace(message))
            {
                _viewModel.InputText = message;
                await _viewModel.SendMessageCommand.ExecuteAsync(null);
                _inputTextBox.Clear();
            }

            // Handle attachment
            if (_attachment != null)
            {
                // For now, just add a message about the attachment
                // In the future, this could upload to storage and include a link
                var attachmentMsg = $"📎 Attachment: {_attachmentName}";
                _viewModel.InputText = attachmentMsg;
                await _viewModel.SendMessageCommand.ExecuteAsync(null);
                ClearAttachment();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sending message: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AttachButton_Click(object? sender, EventArgs e)
    {
        _fileDialog.Reset();
        _fileDialog.Multiselect = false;

        var result = _fileDialog.ShowDialog(this);
        if (result != DialogResult.OK)
            return;

        try
        {
            var filePath = _fileDialog.FileName;
            var fileBytes = File.ReadAllBytes(filePath);

            // Limit attachment size to 1.45 MB (similar to reference implementation)
            if (fileBytes.Length > 1_450_000)
            {
                MessageBox.Show(
                    $"The file '{_fileDialog.SafeFileName}' is too large (max 1.45 MB). Please select a smaller file.",
                    "File Too Large",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _attachment = fileBytes;
            _attachmentName = _fileDialog.SafeFileName;

            // Update UI
            var displayName = _attachmentName;
            if (displayName.Length > 12)
            {
                var name = Path.GetFileNameWithoutExtension(displayName);
                var ext = Path.GetExtension(displayName);
                displayName = name[..Math.Min(7, name.Length)] + ".." + ext;
            }

            _attachButton.Text = displayName;
            _attachButton.Width = 120;
            _removeButton.Visible = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading file: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        ClearAttachment();
    }

    private void ClearAttachment()
    {
        _attachment = null;
        _attachmentName = null;
        _attachButton.Text = "📎";
        _attachButton.Width = 41;
        _removeButton.Visible = false;
    }

    private async void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        // Shift + Enter to send (like Slack)
        if (e.Shift && e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendButton_Click(sender, e);
            await Task.CompletedTask;
        }
    }

    private void InputTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.InputText = _inputTextBox.Text;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Message list auto-handles resizing via its internal logic
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
            }

            _fileDialog?.Dispose();
            _messageList?.Dispose();
            _sendButton?.Dispose();
            _attachButton?.Dispose();
            _removeButton?.Dispose();
            _inputTextBox?.Dispose();
            _topPanel?.Dispose();
            _bottomPanel?.Dispose();
            _titleLabel?.Dispose();
            _statusLabel?.Dispose();
            _contextLabel?.Dispose();
        }

        base.Dispose(disposing);
    }
}
