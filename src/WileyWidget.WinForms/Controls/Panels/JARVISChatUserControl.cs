using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
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

namespace WileyWidget.WinForms.Controls.Panels
{
  /// <summary>
  /// Native Syncfusion AI Assist chat panel backed by the existing JARVIS bridge.
  /// </summary>
  public partial class JARVISChatUserControl : ScopedPanelBase<JARVISChatViewModel>, IAsyncInitializable, IParameterizedPanel
  {
    private static readonly Size SidebarCompatibleMinimumSize = new(320, 420);
    private readonly SyncfusionControlFactory _factory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly StringBuilder _streamingResponse = new();
    private readonly ObservableCollection<object> _messages = new();
    private readonly ObservableCollection<string> _suggestions = new();
    private readonly Author _assistantAuthor = new() { Name = "JARVIS" };
    private readonly Author _userAuthor = new() { Name = "You" };
    private SfAIAssistView? _assistView;
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
          automationState.MarkBlazorReady(assistViewReady: true);
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
      _assistView = _factory.CreateSfAIAssistView(control =>
      {
        control.Name = "JarvisAssistView";
        control.AccessibleName = "JARVIS Assistant";
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
      Controls.Add(_assistView);
      _assistView.BringToFront();
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
      }

      _streamingResponse.Append(e.Chunk);
      _activeResponseMessage.Text = _streamingResponse.ToString();
      _activeResponseMessage.DateTime = DateTime.Now;
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
      GetAutomationStateService()?.NotifyPrompt(prompt.Trim());
      BeginResponse();
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
      if (_assistView != null)
      {
        _assistView.ShowTypingIndicator = true;
      }
    }

    private void EndResponse()
    {
      _isAwaitingResponse = false;
      _activeResponseMessage = null;
      _streamingResponse.Clear();
      if (_assistView != null)
      {
        _assistView.ShowTypingIndicator = false;
        TryScrollAssistViewToBottom();
      }
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
      TryScrollAssistViewToBottom();
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
      Size = new Size(400, 600);
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
