using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Syncfusion.WinForms.Controls;
using WileyWidget.Abstractions;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Services.AI;

namespace WileyWidget.WinForms.Controls.Supporting
{
    /// <summary>
    /// Hosted JARVIS chat control using pure JS/WebView2 for maximum stability.
    /// This control is managed by ScopedPanelBase and initialized via IAsyncInitializable.
    /// </summary>
    public partial class JARVISChatUserControl : ScopedPanelBase, IAsyncInitializable, IParameterizedPanel
    {
        private WebView2? _webView;
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private string _currentStreamingText = string.Empty;
        private bool _isStreaming = false;

        /// <summary>
        /// Gets or sets the initial prompt to be sent to JARVIS.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? InitialPrompt { get; set; }

        public JARVISChatUserControl(
            IServiceScopeFactory scopeFactory,
            IServiceProvider serviceProvider,
            ILogger<JARVISChatUserControl> logger)
            : base(scopeFactory, logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            this.Dock = DockStyle.Fill;
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
            if (_isInitialized || IsDisposed) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_isInitialized || IsDisposed) return;

                Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing WebView2 panel host...");
                await InitializeWebViewAsync(ct);

                // Ensure AI Service is initialized in this scope
                var aiService = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(ServiceProvider) : null)
                                ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IAIService>(_serviceProvider);

                if (aiService is IAsyncInitializable asyncInitAI)
                {
                    Logger?.LogInformation("[JARVIS-LIFECYCLE] Initializing Grok AI service...");
                    await asyncInitAI.InitializeAsync(ct);
                }

                // Activate the bridge handler by resolving it from the scoped provider
                // This ensures its constructor runs and it subscribes to the bridge events for this panel's lifecycle.
                _ = (ServiceProvider != null ? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(ServiceProvider) : null)
                    ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<JarvisGrokBridgeHandler>(_serviceProvider);

                _isInitialized = true;
                Logger?.LogInformation("[JARVIS-LIFECYCLE] JARVIS WebView2 panel initialization successful");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "[JARVIS-LIFECYCLE] Failed to initialize JARVIS WebView2 panel");
                ShowError(ex.Message);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task InitializeWebViewAsync(CancellationToken ct)
        {
            if (InvokeRequired)
            {
                var tcs = new TaskCompletionSource();
                BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await InitializeWebViewInternal();
                        tcs.SetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                await tcs.Task;
            }
            else
            {
                await InitializeWebViewInternal();
            }
        }

        private async Task InitializeWebViewInternal()
        {
            if (IsDisposed) return;

            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = true
            };

            this.Controls.Clear();
            this.Controls.Add(_webView);

            await _webView.EnsureCoreWebView2Async();

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "jarvis.html");
            if (!File.Exists(htmlPath))
            {
                Logger?.LogError("[JARVIS-INIT] jarvis.html not found at: {Path}", htmlPath);
                throw new FileNotFoundException("JARVIS frontend assets missing", htmlPath);
            }

            _webView.Source = new Uri(htmlPath);

            var chatBridge = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IChatBridgeService>(_serviceProvider);
            var themeService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IThemeService>(_serviceProvider);

            themeService.ThemeChanged += (s, themeName) => SyncThemeWithJS(themeName, themeService.IsDark);

            // Handle initial prompt via DOMContentLoaded to ensure UI is ready
            _webView.CoreWebView2.DOMContentLoaded += async (s, e) =>
            {
                SyncThemeWithJS(themeService.CurrentTheme, themeService.IsDark);

                if (!string.IsNullOrWhiteSpace(InitialPrompt))
                {
                    var promptToSend = InitialPrompt;
                    InitialPrompt = null;
                    Logger?.LogInformation("[JARVIS-INIT] Sending initial prompt: {Length} chars", promptToSend.Length);

                    // Inject user message into CSS and trigger Grok
                    var escaped = promptToSend.Replace("`", "\\`").Replace("$", "\\$");
                    await _webView.CoreWebView2.ExecuteScriptAsync(
                        $"addMessage(`{escaped}`, 'user');" +
                        $"document.getElementById('typing').style.display = 'block';" +
                        $"messagesContainer.scrollTop = messagesContainer.scrollHeight;"
                    );

                    await chatBridge.RequestExternalPromptAsync(promptToSend);
                }
                else
                {
                    // Send standard welcome if no initial prompt
                    PostMessageToJS("response", "Welcome to JARVIS. I'm ready to assist you.");
                }
            };

            // Wire up streaming events from Grok
            chatBridge.ResponseChunkReceived += (s, args) =>
            {
                if (!_isStreaming)
                {
                    _isStreaming = true;
                    _currentStreamingText = string.Empty;
                }
                _currentStreamingText += args.Chunk;
                PostMessageToJS("stream", _currentStreamingText, done: false);
            };

            chatBridge.ResponseCompleted += (s, args) =>
            {
                if (_isStreaming)
                {
                    PostMessageToJS("stream", _currentStreamingText, done: true);
                    _isStreaming = false;
                }
            };

            chatBridge.OnMessageReceived += (s, msg) =>
            {
                // Full message received (non-streaming)
                PostMessageToJS("response", msg.Content);
            };

            _webView.CoreWebView2.WebMessageReceived += (s, e) =>
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
                    if (msg.TryGetProperty("type", out var typeProp))
                    {
                        var type = typeProp.GetString();
                        if (type == "prompt")
                        {
                            var content = msg.GetProperty("content").GetString();
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                _isStreaming = false; // Reset streaming state for new prompt
                                _currentStreamingText = string.Empty;
                                _ = chatBridge.RequestExternalPromptAsync(content);
                            }
                        }
                        else if (type == "log")
                        {
                            var level = msg.GetProperty("level").GetString();
                            var content = msg.GetProperty("content").GetString();
                            if (level == "error")
                                Logger?.LogError("[JARVIS-JS] {Content}", content);
                            else
                                Logger?.LogInformation("[JARVIS-JS] {Content}", content);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "[JARVIS-JS] Error processing JSON from JS");
                }
            };
        }

        private void SyncThemeWithJS(string themeName, bool isDark)
        {
            if (_webView?.CoreWebView2 == null || IsDisposed) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => SyncThemeWithJS(themeName, isDark)));
                    return;
                }

                // Construct a theme message with specific color overrides if needed
                // For now we just signal dark mode toggle and let JS handle the rest
                var themeMsg = new
                {
                    type = "theme",
                    isDark = isDark,
                    themeName = themeName
                };

                var json = JsonSerializer.Serialize(themeMsg);
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                Logger?.LogInformation("[JARVIS-THEME] Synced theme {Theme} (IsDark: {IsDark}) to JS", themeName, isDark);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-THEME] Failed to sync theme to WebView2");
            }
        }

        private void PostMessageToJS(string type, string content, bool done = false)
        {
            if (_webView?.CoreWebView2 == null || IsDisposed) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => PostMessageToJS(type, content, done)));
                    return;
                }

                var msgJson = JsonSerializer.Serialize(new { type, content, done });
                _webView.CoreWebView2.PostWebMessageAsJson(msgJson);
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "[JARVIS-BRIDGE] Failed to post message to WebView2");
            }
        }

        private void ShowError(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowError(message)));
                return;
            }

            this.Controls.Clear();
            this.Controls.Add(new Label
            {
                Text = $"Error: {message}",
                ForeColor = Color.Red,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "JARVISChatUserControl";
            this.Size = new Size(400, 600);
            this.BackColor = Color.FromArgb(32, 32, 32); // Fallback color
            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
                _initLock?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class JARVISChatViewModel { }
}
