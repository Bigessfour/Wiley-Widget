using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.WinForms.AIAssistView;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Automation;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.AI;
using WileyWidget.WinForms.Themes;
using WileyWidget.WinForms.UI.Helpers;
using ProgressBarAdv = Syncfusion.Windows.Forms.Tools.ProgressBarAdv;
using ProgressBarStyles = Syncfusion.Windows.Forms.Tools.ProgressBarStyles;

namespace WileyWidget.WinForms.Controls.Panels
{
  /// <summary>
  /// Native Syncfusion AI Assist chat panel backed by the existing JARVIS bridge.
  /// </summary>
  public partial class JARVISChatUserControl : ScopedPanelBase<JARVISChatViewModel>, IAsyncInitializable, IParameterizedPanel
  {
    private static readonly Size SidebarCompatibleMinimumSize = new(320, 520);
    private readonly SyncfusionControlFactory _factory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly StringBuilder _streamingResponse = new();
    private readonly ObservableCollection<object> _messages = new();
    private readonly ObservableCollection<string> _suggestions = new();
    private readonly Author _assistantAuthor = new() { Name = "JARVIS" };
    private readonly Author _userAuthor = new() { Name = "You" };
    private SfAIAssistView? _assistView;
    private Panel? _assistHost;
    private Panel? _toolbarPanel;
    private TableLayoutPanel? _responseStatusPanel;
    private Label? _responseStatusLabel;
    private ProgressBarAdv? _responseStatusProgressBar;
    private SfButton? _openResponseViewerButton;
    private SfButton? _copyLatestResponseButton;
    private SfButton? _copyTranscriptButton;
    private SfButton? _exportTranscriptButton;
    private ToolTip? _toolTip;
    private TextMessage? _activeResponseMessage;
    private IChatBridgeService? _chatBridge;
    private JarvisGrokBridgeHandler? _bridgeHandler;
    private IThemeService? _themeService;
    private EventHandler<string>? _themeChangedHandler;
    private bool _isInitialized;
    private bool _isAwaitingResponse;
    private bool _isResponseCancelled;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? InitialPrompt { get; set; }

    public TextBox? AutomationStatusBox { get; private set; }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public JARVISChatUserControl(
        JARVISChatViewModel viewModel,
        SyncfusionControlFactory controlFactory,
        IServiceProvider serviceProvider,
        ILogger<JARVISChatUserControl> logger)
        : base(viewModel, controlFactory, logger)
    {
      _factory = controlFactory ?? throw new ArgumentNullException(nameof(controlFactory));
      _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
      SafeSuspendAndLayout(InitializeComponent);
      BuildPanelUi();
      EnsureAutomationStatusBoxPresent();
      CompleteDirectInitialization();
    }

    public void InitializeWithParameters(object parameters)
    {
      if (parameters is string prompt)
      {
        InitialPrompt = prompt;
        Logger?.LogInformation("[JARVIS-PARAM] Initial prompt set: {Length} chars", prompt.Length);
      }
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
      if (_isInitialized || IsDisposed)
      {
        return;
      }

      var lockAcquired = false;
      try
      {
        await _initLock.WaitAsync(ct);
        lockAcquired = true;

        if (_isInitialized || IsDisposed)
        {
          return;
        }

        EnsureAutomationStatusBoxPresent();

        var aiService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(_serviceProvider);

        if (aiService is IAsyncInitializable asyncInitializable)
        {
          Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing Grok AI service...");
          await asyncInitializable.InitializeAsync(ct);
        }

        _chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(_serviceProvider);
        _bridgeHandler = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<JarvisGrokBridgeHandler>(_serviceProvider);
        _themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(_serviceProvider);

        _chatBridge.OnMessageReceived += OnMessageReceived;
        _chatBridge.ResponseChunkReceived += OnResponseChunkReceived;
        _chatBridge.ResponseCompleted += OnResponseCompleted;

        _themeChangedHandler = (_, _) => ApplyCurrentTheme();
        _themeService.ThemeChanged += _themeChangedHandler;

        var automationState = GetAutomationStateService();
        if (automationState != null && AutomationStatusBox != null)
        {
          AutomationStatusBox.Text = automationState.Snapshot.ToStatusString();
          automationState.Changed += (s, e) =>
          {
            if (IsDisposed || AutomationStatusBox == null)
            {
              return;
            }

            if (InvokeRequired)
            {
              BeginInvoke(new Action(() => AutomationStatusBox.Text = e.Snapshot.ToStatusString()));
            }
            else
            {
              AutomationStatusBox.Text = e.Snapshot.ToStatusString();
            }
          };

          // Preserve the existing automation contract while the UI host is now native.
          automationState.MarkChatUiReady(assistViewReady: true);
          AutomationStatusBox.Text = automationState.Snapshot.ToStatusString();
        }

        _isInitialized = true;
        ApplyCurrentTheme();

        if (!string.IsNullOrWhiteSpace(InitialPrompt))
        {
          var prompt = InitialPrompt;
          InitialPrompt = null;
          await SubmitExternalPromptAsync(prompt!, ct);
        }

        Logger?.LogInformation("[JARVIS-LIFECYCLE] Native JARVIS panel initialization successful");
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        Logger?.LogInformation("[JARVIS-LIFECYCLE] Native JARVIS panel initialization canceled");
      }
      catch (Exception ex)
      {
        Logger?.LogError(ex, "[JARVIS-LIFECYCLE] Failed to initialize native JARVIS panel");
        ShowError(ex.Message);
      }
      finally
      {
        if (lockAcquired)
        {
          _initLock.Release();
        }
      }
    }

    private void BuildPanelUi()
    {
      _toolTip ??= new ToolTip();

      _toolbarPanel = new Panel
      {
        Name = "JarvisToolbarPanel",
        AccessibleName = "JARVIS conversation actions",
        Dock = DockStyle.Top,
        Height = 72,
        Padding = Padding.Empty
      };

      var toolbarLayout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 2,
        Margin = Padding.Empty,
        Padding = new Padding(8, 6, 8, 6)
      };
      toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

      var toolbarHintLabel = new Label
      {
        Name = "JarvisToolbarHintLabel",
        AccessibleName = "JARVIS action hint",
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        Margin = new Padding(0, 0, 0, 4),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "Open, copy, or export JARVIS responses."
      };

      var toolbarActionsPanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoScroll = false,
        Margin = Padding.Empty,
        Padding = Padding.Empty
      };

      _openResponseViewerButton = CreateToolbarButton(
        "JarvisOpenResponseViewerButton",
        "Open Reply",
        "Open the full JARVIS reply in a larger viewer",
        OnOpenResponseViewerClicked);
      _toolTip.SetToolTip(_openResponseViewerButton, "Open the most recent JARVIS reply in a larger window.");

      _copyLatestResponseButton = CreateToolbarButton(
        "JarvisCopyLatestResponseButton",
        "Copy Reply",
        "Copy the most recent JARVIS reply",
        OnCopyLatestResponseClicked);
      _toolTip.SetToolTip(_copyLatestResponseButton, "Copy the most recent JARVIS reply to the clipboard.");

      _copyTranscriptButton = CreateToolbarButton(
        "JarvisCopyTranscriptButton",
        "Copy Chat",
        "Copy the full JARVIS conversation transcript",
        OnCopyTranscriptClicked);
      _toolTip.SetToolTip(_copyTranscriptButton, "Copy the full JARVIS conversation transcript.");

      _exportTranscriptButton = CreateToolbarButton(
        "JarvisExportTranscriptButton",
        "Export Chat",
        "Export the full JARVIS conversation transcript",
        OnExportTranscriptClicked);
      _toolTip.SetToolTip(_exportTranscriptButton, "Export the conversation transcript to a text or markdown file.");

      toolbarActionsPanel.Controls.Add(_openResponseViewerButton);
      toolbarActionsPanel.Controls.Add(_copyLatestResponseButton);
      toolbarActionsPanel.Controls.Add(_copyTranscriptButton);
      toolbarActionsPanel.Controls.Add(_exportTranscriptButton);

      toolbarLayout.Controls.Add(toolbarHintLabel, 0, 0);
      toolbarLayout.Controls.Add(toolbarActionsPanel, 0, 1);
      _toolbarPanel.Controls.Add(toolbarLayout);

      _assistHost = new Panel
      {
        Name = "JarvisAssistHost",
        AccessibleName = "JARVIS assist host",
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = new Padding(0, 0, 0, (int)Syncfusion.Windows.Forms.DpiAware.LogicalToDeviceUnits(24.0f))
      };

      _responseStatusPanel = new TableLayoutPanel
      {
        Name = "JarvisResponseStatusPanel",
        AccessibleName = "JARVIS response status",
        Dock = DockStyle.Top,
        Height = 28,
        ColumnCount = 2,
        RowCount = 1,
        Margin = Padding.Empty,
        Padding = new Padding(12, 4, 12, 4),
        Visible = false
      };
      _responseStatusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      _responseStatusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      _responseStatusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

      _responseStatusProgressBar = _factory.CreateProgressBarAdv(progressBar =>
      {
        progressBar.Name = "JarvisResponseStatusProgress";
        progressBar.AccessibleName = "JARVIS response progress";
        progressBar.AutoSize = false;
        progressBar.Size = new Size(96, 12);
        progressBar.MinimumSize = new Size(96, 12);
        progressBar.Margin = new Padding(0, 4, 8, 0);
        progressBar.ProgressStyle = ProgressBarStyles.WaitingGradient;
        progressBar.WaitingGradientWidth = 10;
        progressBar.Value = 50;
        progressBar.TextVisible = false;
        progressBar.TextShadow = false;
        progressBar.Visible = false;
      });

      _responseStatusLabel = new Label
      {
        Name = "JarvisResponseStatusLabel",
        AccessibleName = "JARVIS response status text",
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        Margin = Padding.Empty,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "JARVIS is thinking..."
      };

      _responseStatusPanel.Controls.Add(_responseStatusProgressBar, 0, 0);
      _responseStatusPanel.Controls.Add(_responseStatusLabel, 1, 0);

      _assistView = _factory.CreateSfAIAssistView(control =>
      {
        control.Name = "JarvisAssistView";
        control.AccessibleName = "JARVIS Assistant";
        control.AccessibleDescription = "Native Syncfusion AI Assist chat surface for JARVIS conversations";
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        control.Padding = Padding.Empty;
        control.MinimumSize = Size.Empty;
      });

      _assistView.User = _userAuthor;
      _assistView.Messages = _messages;
      _assistView.Suggestions = _suggestions;
      _assistView.TypingIndicator = new TypingIndicator
      {
        Author = _assistantAuthor,
        DisplayText = "JARVIS is thinking..."
      };
      _assistView.SetBannerView("JARVIS Assistant", "Ask me anything about your municipality", null!, new BannerStyle());
      _assistView.PromptRequest += OnPromptRequest;
      _assistView.SuggestionSelected += OnSuggestionSelected;
      AttachOptionalAssistViewEvent("StopResponding", nameof(OnStopResponding));

      _suggestions.Add("Summarize today's activity log");
      _suggestions.Add("What changed in QuickBooks imports?");
      _suggestions.Add("Show likely budget risks this month");

      Controls.Clear();
      _assistHost.Controls.Add(_assistView);
      _assistHost.Controls.Add(_responseStatusPanel);
      Controls.Add(_assistHost);
      Controls.Add(_toolbarPanel);
      _responseStatusPanel.BringToFront();
      UpdateResponseActionStates();
    }

    private SfButton CreateToolbarButton(string name, string text, string accessibleDescription, EventHandler onClick)
    {
      var button = _factory.CreateSfButton(text, control =>
      {
        control.Name = name;
        control.AutoSize = false;
      }, SyncfusionControlFactory.SfButtonLayoutProfile.Toolbar);

      button.AccessibleName = text;
      button.AccessibleDescription = accessibleDescription;
      button.Click += onClick;
      return button;
    }

    private async void OnPromptRequest(object? sender, PromptRequestEventArgs e)
    {
      var prompt = e.Message?.Text?.Trim();
      if (string.IsNullOrWhiteSpace(prompt))
      {
        return;
      }

      try
      {
        Logger?.LogInformation("[JARVIS] Prompt requested from SfAIAssistView ({Length} chars)", prompt.Length);
        e.Handled = true;

        if (!await EnsureChatRuntimeReadyAsync(CancellationToken.None))
        {
          throw new InvalidOperationException("JARVIS chat runtime is not available.");
        }

        GetAutomationStateService()?.NotifyPrompt(prompt);
        BeginResponse();
        await FlushPendingStateAsync();
        await _chatBridge!.RequestExternalPromptAsync(prompt);
      }
      catch (Exception ex)
      {
        Logger?.LogError(ex, "[JARVIS] Prompt submission failed");
        AppendAssistantMessage($"Error: {ex.Message}");
        EndResponse();
      }
    }

    private async void OnSuggestionSelected(object? sender, SuggestionSelectedEventArgs e)
    {
      try
      {
        if (_chatBridge != null)
        {
          await _chatBridge.NotifySuggestionSelectedAsync(Convert.ToString(e.Item) ?? string.Empty).ConfigureAwait(false);
        }
      }
      catch (Exception ex)
      {
        Logger?.LogWarning(ex, "[JARVIS] Suggestion selection notification failed");
      }
    }

    private void OnStopResponding(object? sender, EventArgs e)
    {
      _isResponseCancelled = true;
      EndResponse();
      Logger?.LogInformation("[JARVIS] Stop responding requested by user");
    }

    private void OnMessageReceived(object? sender, WileyWidget.Models.ChatMessage message)
    {
      if (message == null || message.IsUser || string.IsNullOrWhiteSpace(message.Content))
      {
        return;
      }

      if (_isAwaitingResponse && _activeResponseMessage == null)
      {
        AppendAssistantMessage(message.Content);
        EndResponse();
      }
    }

    private void OnResponseChunkReceived(object? sender, ChatResponseChunkEventArgs e)
    {
      if (_assistView == null || _isResponseCancelled || string.IsNullOrEmpty(e.Chunk))
      {
        return;
      }

      if (InvokeRequired)
      {
        BeginInvoke(new Action(() => OnResponseChunkReceived(sender, e)));
        return;
      }

      if (_activeResponseMessage == null)
      {
        _activeResponseMessage = CreateMessage(string.Empty, _assistantAuthor);
        _messages.Add(_activeResponseMessage);
        SetPendingStateVisible(false);
      }

      _streamingResponse.Append(e.Chunk);
      _activeResponseMessage.Text = _streamingResponse.ToString();
      _activeResponseMessage.DateTime = DateTime.Now;
      UpdateResponseActionStates();
      TryScrollAssistViewToBottom();
    }

    private void OnResponseCompleted(object? sender, EventArgs e)
    {
      if (InvokeRequired)
      {
        BeginInvoke(new Action(() => OnResponseCompleted(sender, e)));
        return;
      }

      if (_streamingResponse.Length > 0)
      {
        GetAutomationStateService()?.NotifyResponse(_streamingResponse.ToString());
      }

      EndResponse();
      GetAutomationStateService()?.MarkDiagnosticsCompleted();
    }

    private async Task SubmitExternalPromptAsync(string prompt, CancellationToken cancellationToken)
    {
      if (_assistView == null || string.IsNullOrWhiteSpace(prompt))
      {
        return;
      }

      if (InvokeRequired)
      {
        var tcs = new TaskCompletionSource();
        BeginInvoke(new Action(async () =>
        {
          try
          {
            await SubmitExternalPromptAsync(prompt, cancellationToken).ConfigureAwait(false);
            tcs.SetResult();
          }
          catch (Exception ex)
          {
            tcs.SetException(ex);
          }
        }));
        await tcs.Task.ConfigureAwait(false);
        return;
      }

      if (!await EnsureChatRuntimeReadyAsync(cancellationToken).ConfigureAwait(true))
      {
        ShowError("JARVIS chat runtime is not available.");
        return;
      }

      _messages.Add(CreateMessage(prompt.Trim(), _userAuthor));
      UpdateResponseActionStates();
      GetAutomationStateService()?.NotifyPrompt(prompt.Trim());
      BeginResponse();
      await FlushPendingStateAsync().ConfigureAwait(true);
      await _chatBridge!.RequestExternalPromptAsync(prompt.Trim(), cancellationToken).ConfigureAwait(true);
    }

    private async Task<bool> EnsureChatRuntimeReadyAsync(CancellationToken cancellationToken)
    {
      if (IsDisposed)
      {
        return false;
      }

      if (!_isInitialized)
      {
        await InitializeAsync(cancellationToken).ConfigureAwait(true);
      }

      return _isInitialized && _chatBridge != null && _bridgeHandler != null;
    }

    private void BeginResponse()
    {
      _isAwaitingResponse = true;
      _isResponseCancelled = false;
      _streamingResponse.Clear();
      _activeResponseMessage = null;
      SetPendingStateVisible(true, "JARVIS is thinking...");
    }

    private void EndResponse()
    {
      _isAwaitingResponse = false;
      _activeResponseMessage = null;
      _streamingResponse.Clear();
      SetPendingStateVisible(false);
      if (_assistView != null)
      {
        TryScrollAssistViewToBottom();
      }

      UpdateResponseActionStates();
    }

    private void SetPendingStateVisible(bool isVisible, string statusText = "JARVIS is thinking...")
    {
      if (_responseStatusLabel != null)
      {
        _responseStatusLabel.Text = statusText;
      }

      if (_responseStatusPanel != null)
      {
        _responseStatusPanel.Visible = isVisible;
      }

      if (_responseStatusProgressBar != null)
      {
        _responseStatusProgressBar.Visible = isVisible;
      }

      if (_assistView != null)
      {
        _assistView.ShowTypingIndicator = isVisible;
      }

      _responseStatusPanel?.PerformLayout();
      _responseStatusPanel?.Invalidate(true);
      _responseStatusPanel?.Update();
      _assistHost?.PerformLayout();
      _assistHost?.Invalidate(true);
      _assistHost?.Update();
      _assistView?.Invalidate(true);
      _assistView?.Update();
    }

    private async Task FlushPendingStateAsync()
    {
      if (IsDisposed)
      {
        return;
      }

      _responseStatusPanel?.Refresh();
      _assistHost?.Refresh();
      _assistView?.Refresh();
      Refresh();
      await Task.Yield();
    }

    private void AppendAssistantMessage(string content)
    {
      if (_assistView == null || string.IsNullOrWhiteSpace(content))
      {
        return;
      }

      if (InvokeRequired)
      {
        BeginInvoke(new Action(() => AppendAssistantMessage(content)));
        return;
      }

      _messages.Add(CreateMessage(content, _assistantAuthor));
      UpdateResponseActionStates();
      TryScrollAssistViewToBottom();
    }

    private void OnOpenResponseViewerClicked(object? sender, EventArgs e)
    {
      var latestResponse = GetLatestAssistantResponseText();
      var transcript = GetConversationTranscriptText();

      var viewerText = !string.IsNullOrWhiteSpace(latestResponse)
        ? latestResponse
        : transcript;

      if (string.IsNullOrWhiteSpace(viewerText))
      {
        return;
      }

      ShowResponseViewer(viewerText, !string.IsNullOrWhiteSpace(latestResponse)
        ? "Most recent JARVIS reply"
        : "Conversation transcript");
    }

    private void OnCopyLatestResponseClicked(object? sender, EventArgs e)
    {
      var latestResponse = GetLatestAssistantResponseText();
      if (string.IsNullOrWhiteSpace(latestResponse))
      {
        return;
      }

      try
      {
        ClipboardHelper.CopyText(latestResponse, Logger);
        ShowTransientButtonState(_copyLatestResponseButton, "Copied!");
      }
      catch (Exception ex)
      {
        Logger?.LogWarning(ex, "[JARVIS] Failed to copy latest response");
        SfDialogHelper.ShowWarningDialog(FindForm(), "Copy failed", ex.Message, Logger);
      }
    }

    private void OnCopyTranscriptClicked(object? sender, EventArgs e)
    {
      var transcript = GetConversationTranscriptText();
      if (string.IsNullOrWhiteSpace(transcript))
      {
        return;
      }

      try
      {
        ClipboardHelper.CopyText(transcript, Logger);
        ShowTransientButtonState(_copyTranscriptButton, "Copied!");
      }
      catch (Exception ex)
      {
        Logger?.LogWarning(ex, "[JARVIS] Failed to copy transcript");
        SfDialogHelper.ShowWarningDialog(FindForm(), "Copy failed", ex.Message, Logger);
      }
    }

    private async void OnExportTranscriptClicked(object? sender, EventArgs e)
    {
      var transcript = GetConversationTranscriptText();
      if (string.IsNullOrWhiteSpace(transcript))
      {
        return;
      }

      var result = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
        FindForm(),
        "JarvisChat.ExportTranscript",
        "Export JARVIS Conversation",
        "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
        "txt",
        $"JarvisChat_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        (filePath, cancellationToken) => File.WriteAllTextAsync(filePath, transcript, Encoding.UTF8, cancellationToken),
        logger: Logger,
        cancellationToken: CancellationToken.None);

      if (result.IsSuccess)
      {
        ShowTransientButtonState(_exportTranscriptButton, "Exported!");
        return;
      }

      if (!result.IsCancelled && !result.IsSkipped && !string.IsNullOrWhiteSpace(result.ErrorMessage))
      {
        SfDialogHelper.ShowWarningDialog(FindForm(), "Export failed", result.ErrorMessage, Logger);
      }
    }

    private void ShowTransientButtonState(SfButton? button, string temporaryText)
    {
      if (button == null || button.IsDisposed)
      {
        return;
      }

      var originalText = button.Text;
      button.Text = temporaryText;
      button.Enabled = false;

      var timer = new System.Windows.Forms.Timer { Interval = 1500 };
      timer.Tick += (_, _) =>
      {
        timer.Stop();
        timer.Dispose();

        if (!button.IsDisposed)
        {
          button.Text = originalText;
          UpdateResponseActionStates();
        }
      };
      timer.Start();
    }

    private void UpdateResponseActionStates()
    {
      var latestResponse = GetLatestAssistantResponseText();
      var transcript = GetConversationTranscriptText();

      var hasLatestResponse = !string.IsNullOrWhiteSpace(latestResponse);
      var hasTranscript = !string.IsNullOrWhiteSpace(transcript);

      if (_openResponseViewerButton != null)
      {
        _openResponseViewerButton.Enabled = hasLatestResponse || hasTranscript;
      }

      if (_copyLatestResponseButton != null)
      {
        _copyLatestResponseButton.Enabled = hasLatestResponse;
      }

      if (_copyTranscriptButton != null)
      {
        _copyTranscriptButton.Enabled = hasTranscript;
      }

      if (_exportTranscriptButton != null)
      {
        _exportTranscriptButton.Enabled = hasTranscript;
      }
    }

    private string GetLatestAssistantResponseText()
    {
      if (_activeResponseMessage != null && !string.IsNullOrWhiteSpace(_activeResponseMessage.Text))
      {
        return _activeResponseMessage.Text;
      }

      return _messages
        .OfType<TextMessage>()
        .Where(message => string.Equals(message.Author?.Name, _assistantAuthor.Name, StringComparison.OrdinalIgnoreCase))
        .Select(message => message.Text)
        .LastOrDefault(text => !string.IsNullOrWhiteSpace(text))
        ?? string.Empty;
    }

    private string GetConversationTranscriptText()
    {
      if (_messages.Count == 0)
      {
        return string.Empty;
      }

      var builder = new StringBuilder();
      builder.AppendLine("JARVIS Conversation Transcript");
      builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      builder.AppendLine();

      foreach (var message in _messages.OfType<TextMessage>())
      {
        var authorName = message.Author?.Name ?? "Unknown";
        var timestamp = message.DateTime == default ? DateTime.Now : message.DateTime;
        builder.AppendLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] {authorName}");
        builder.AppendLine(message.Text ?? string.Empty);
        builder.AppendLine();
      }

      return builder.ToString().TrimEnd();
    }

    private void ShowResponseViewer(string content, string subtitle)
    {
      using var viewer = new SfForm
      {
        Name = "JarvisResponseViewer",
        Text = "JARVIS Response Viewer",
        StartPosition = FormStartPosition.CenterParent,
        Size = new Size(960, 720),
        MinimumSize = new Size(700, 520),
        ShowIcon = false,
        ShowInTaskbar = false
      };

      try
      {
        viewer.ApplySyncfusionTheme(_themeService?.CurrentTheme ?? ThemeColors.DefaultTheme, Logger);
      }
      catch (Exception ex)
      {
        Logger?.LogDebug(ex, "[JARVIS] Failed to apply theme to response viewer");
      }

      var headerLabel = new Label
      {
        Name = "JarvisResponseViewerHeader",
        Dock = DockStyle.Top,
        Height = 36,
        Padding = new Padding(12, 10, 12, 0),
        Text = subtitle,
        TextAlign = ContentAlignment.MiddleLeft
      };

      var responseTextBox = new TextBox
      {
        Name = "JarvisResponseViewerText",
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = true,
        ShortcutsEnabled = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Regular),
        Text = content
      };

      var buttonPanel = new Panel
      {
        Name = "JarvisResponseViewerButtonPanel",
        Dock = DockStyle.Bottom,
        Height = 48,
        Padding = new Padding(12, 8, 12, 8)
      };

      var closeButton = _factory.CreateSfButton("Close", button =>
      {
        button.Name = "JarvisResponseViewerCloseButton";
        button.Dock = DockStyle.Right;
      }, SyncfusionControlFactory.SfButtonLayoutProfile.Toolbar);
      closeButton.Click += (_, _) => viewer.Close();

      var exportButton = _factory.CreateSfButton("Export", button =>
      {
        button.Name = "JarvisResponseViewerExportButton";
        button.Dock = DockStyle.Right;
      }, SyncfusionControlFactory.SfButtonLayoutProfile.Toolbar);
      exportButton.Click += async (_, _) =>
      {
        var exportResult = await ExportWorkflowService.ExecuteWithSaveDialogAsync(
          viewer,
          "JarvisChat.ExportViewerText",
          "Export JARVIS Response",
          "Text Files (*.txt)|*.txt|Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
          "txt",
          $"JarvisReply_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
          (filePath, cancellationToken) => File.WriteAllTextAsync(filePath, responseTextBox.Text, Encoding.UTF8, cancellationToken),
          logger: Logger,
          cancellationToken: CancellationToken.None);

        if (exportResult.IsSuccess)
        {
          ShowTransientButtonState(exportButton, "Exported!");
        }
      };

      var copyButton = _factory.CreateSfButton("Copy", button =>
      {
        button.Name = "JarvisResponseViewerCopyButton";
        button.Dock = DockStyle.Right;
      }, SyncfusionControlFactory.SfButtonLayoutProfile.Toolbar);
      copyButton.Click += (_, _) =>
      {
        try
        {
          ClipboardHelper.CopyText(responseTextBox.Text, Logger);
          ShowTransientButtonState(copyButton, "Copied!");
        }
        catch (Exception ex)
        {
          Logger?.LogWarning(ex, "[JARVIS] Failed to copy response viewer text");
          SfDialogHelper.ShowWarningDialog(viewer, "Copy failed", ex.Message, Logger);
        }
      };

      buttonPanel.Controls.Add(closeButton);
      buttonPanel.Controls.Add(exportButton);
      buttonPanel.Controls.Add(copyButton);

      viewer.Controls.Add(responseTextBox);
      viewer.Controls.Add(buttonPanel);
      viewer.Controls.Add(headerLabel);

      var owner = FindForm();
      if (owner != null)
      {
        viewer.ShowDialog(owner);
      }
      else
      {
        viewer.ShowDialog();
      }
    }

    private void TryScrollAssistViewToBottom()
    {
      var method = _assistView?.GetType().GetMethod("ScrollToBottom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      method?.Invoke(_assistView, Array.Empty<object>());
    }

    private void AttachOptionalAssistViewEvent(string eventName, string handlerName)
    {
      if (_assistView == null)
      {
        return;
      }

      var eventInfo = _assistView.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
      var methodInfo = GetType().GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
      if (eventInfo?.EventHandlerType == null || methodInfo == null)
      {
        return;
      }

      var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, methodInfo, throwOnBindFailure: false);
      if (handler != null)
      {
        eventInfo.AddEventHandler(_assistView, handler);
      }
    }

    private void DetachOptionalAssistViewEvent(string eventName, string handlerName)
    {
      if (_assistView == null)
      {
        return;
      }

      var eventInfo = _assistView.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
      var methodInfo = GetType().GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
      if (eventInfo?.EventHandlerType == null || methodInfo == null)
      {
        return;
      }

      var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, methodInfo, throwOnBindFailure: false);
      if (handler != null)
      {
        eventInfo.RemoveEventHandler(_assistView, handler);
      }
    }

    private static TextMessage CreateMessage(string content, Author author)
    {
      return new TextMessage
      {
        Text = content,
        Author = author,
        DateTime = DateTime.Now
      };
    }

    private void ApplyCurrentTheme()
    {
      var themeName = _themeService?.CurrentTheme
          ?? SfSkinManager.ApplicationVisualTheme
          ?? ThemeColors.DefaultTheme;

      try
      {
        this.ApplySyncfusionTheme(themeName, Logger);
        _assistView?.ApplySyncfusionTheme(themeName, Logger);
      }
      catch (Exception ex)
      {
        Logger?.LogWarning(ex, "[JARVIS-THEME] Failed to apply theme to native JARVIS control");
      }
    }

    private void ShowError(string message)
    {
      if (InvokeRequired)
      {
        BeginInvoke(new Action(() => ShowError(message)));
        return;
      }

      AppendAssistantMessage($"Error: {message}");
    }

    private void EnsureAutomationStatusBoxPresent()
    {
      if (!ShouldShowAutomationStatusBox())
      {
        RemoveAutomationStatusBox();
        return;
      }

      if (AutomationStatusBox == null || AutomationStatusBox.IsDisposed)
      {
        AutomationStatusBox = new TextBox
        {
          Name = "JarvisAutomationStatus",
          AccessibleName = "JarvisAutomationStatus",
          ReadOnly = true,
          BorderStyle = BorderStyle.None,
          Dock = DockStyle.Bottom,
          Height = 20,
          Visible = true,
          Text = "Automation state: Pending..."
        };
      }

      if (!Controls.Contains(AutomationStatusBox))
      {
        Controls.Add(AutomationStatusBox);
        AutomationStatusBox.BringToFront();
      }
    }

    private void RemoveAutomationStatusBox()
    {
      if (AutomationStatusBox == null)
      {
        return;
      }

      if (Controls.Contains(AutomationStatusBox))
      {
        Controls.Remove(AutomationStatusBox);
      }

      AutomationStatusBox.Dispose();
      AutomationStatusBox = null;
    }

    private bool ShouldShowAutomationStatusBox()
    {
      var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(_serviceProvider);
      if (configuration?.GetValue<bool>("UI:IsUiTestHarness") == true)
      {
        return true;
      }

      return IsTruthy(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS"))
        || IsTruthy(Environment.GetEnvironmentVariable("WILEYWIDGET_UI_AUTOMATION_JARVIS"))
          || IsTruthy(Environment.GetEnvironmentVariable("WILEYWIDGET_TESTS"));
    }

    private static bool IsTruthy(string? value)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        return false;
      }

      return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
          || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private void InitializeComponent()
    {
      SuspendLayout();
      Name = "JARVISChatUserControl";
      Size = new Size(480, 720);
      MinimumSize = SidebarCompatibleMinimumSize;
      AutoScroll = false;
      Padding = Padding.Empty;
      Margin = Padding.Empty;
      Dock = DockStyle.Fill;
      try
      {
        var theme = SfSkinManager.ApplicationVisualTheme ?? ThemeColors.DefaultTheme;
        this.ApplySyncfusionTheme(theme, Logger);
      }
      catch
      {
      }

      ResumeLayout(false);
    }

    private JarvisAutomationState? GetAutomationStateService()
    {
      return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisAutomationState>(_serviceProvider);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
      base.OnHandleCreated(e);
      EnsureAutomationStatusBoxPresent();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        if (_themeService != null && _themeChangedHandler != null)
        {
          _themeService.ThemeChanged -= _themeChangedHandler;
          _themeChangedHandler = null;
        }

        if (_assistView != null)
        {
          _assistView.PromptRequest -= OnPromptRequest;
          _assistView.SuggestionSelected -= OnSuggestionSelected;
          DetachOptionalAssistViewEvent("StopResponding", nameof(OnStopResponding));
        }

        if (_chatBridge != null)
        {
          _chatBridge.OnMessageReceived -= OnMessageReceived;
          _chatBridge.ResponseChunkReceived -= OnResponseChunkReceived;
          _chatBridge.ResponseCompleted -= OnResponseCompleted;
        }

        _assistView?.Dispose();
        _bridgeHandler?.Dispose();
        _initLock.Dispose();
      }

      base.Dispose(disposing);
    }
  }

  public sealed class JARVISChatViewModel : ObservableObject
  {
  }
}
