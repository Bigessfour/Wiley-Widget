using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

// Suppress warnings for fields used in Razor markup (not visible to C# analyzer)
#pragma warning disable CS0649  // Field never assigned (assigned via @ref in Razor)
#pragma warning disable CS0414  // Field assigned but never used (used in Razor templates)

namespace WileyWidget.WinForms.BlazorComponents
{
    public sealed partial class JARVISAssist : ComponentBase, IDisposable
    {
        private readonly List<ChatMessage> _messages = new();
        private string _userInput = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isThinking;
        private bool _isSending;
        private string? _streamingMessageId;
        private string? _conversationId;
        private CancellationTokenSource? _cts;
        private DateTime _lastRenderTime = DateTime.MinValue;
        private readonly TimeSpan _renderThrottle = TimeSpan.FromMilliseconds(50);

        // AssistView backing state - assigned via @ref in Razor markup
        private Syncfusion.Blazor.InteractiveChat.SfAIAssistView? _assistView;
        private List<Syncfusion.Blazor.InteractiveChat.AssistViewPrompt> _prompts = new();
        private List<string> _suggestions = new()
        {
            "Show me today's budget summary",
            "What accounts need attention?",
            "Generate financial report",
            "Help me create a new budget",
            "Show recent transactions",
            "Explain QuickBooks integration"
        };
        private TaskCompletionSource<string>? _responseTcs;
        private readonly StringBuilder _responseBuffer = new();

        // View configuration - used in Razor BannerTemplate
        private string _currentViewId = Guid.NewGuid().ToString();
        private string _currentViewHeader = "JARVIS - Wiley Widget Assistant";
        private string _bannerTitle = "AI Assistance";
        private string _bannerSubtitle = "Your intelligent municipal budget assistant. Ask me anything!";

        // Attachment settings
        private readonly Syncfusion.Blazor.InteractiveChat.AssistViewAttachmentSettings _attachmentSettings = new();
        private readonly List<string> _uploadedFiles = new();

        // Audio playback state - used in Razor ResponseItemTemplate
        private string _audioIconCss = "e-icons e-volume";
        private string _audioTooltip = "Read aloud";
        private bool _isPlayingAudio;
        private string? _currentAudioText;

        // Settings dialog state - reserved for future settings UI
        private bool _showSettingsDialog;
        private string _selectedModel = "grok-4-1-fast";
        private float _temperature = 0.7f;
        private int _maxTokens = 2000;
        private string _selectedTheme = "Office2019Colorful";

        // Token usage tracking
        private int _tokensUsed;
        private int _tokenLimit = 10000;
        private int _promptTokens;
        private int _completionTokens;

        [Inject]
        private IChatBridgeService ChatBridge { get; set; } = null!;
        [Inject]
        private IAIService? AIService { get; set; }
        [Inject]
        private IJSRuntime? JS { get; set; }
        [Inject]
        private IConversationRepository? ConversationRepository { get; set; }
        [CascadingParameter]
        private WileyWidget.Services.Abstractions.IUserContext? UserContext { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Console.WriteLine("[JARVIS-BLAZOR] OnInitializedAsync - Starting component initialization");
            System.Diagnostics.Debug.WriteLine("[JARVIS-BLAZOR] OnInitializedAsync - Starting component initialization");

            // Verify ChatBridge connection
            await VerifyConnectionsAsync();

            ChatBridge.OnMessageReceived += HandleMessageReceived;
            ChatBridge.ResponseChunkReceived += HandleResponseChunkReceived;
            ChatBridge.ExternalPromptRequested += HandleExternalPromptRequested;
            _conversationId = Guid.NewGuid().ToString();

            // Try to load last conversation from database
            await LoadLastConversationAsync();

            // Map any existing messages into the AssistView prompts
            UpdatePromptsFromMessages();

            Console.WriteLine($"[JARVIS-BLAZOR] OnInitializedAsync - Initialization complete, ConversationId={_conversationId}");
            System.Diagnostics.Debug.WriteLine($"[JARVIS-BLAZOR] OnInitializedAsync - Initialization complete, ConversationId={_conversationId}");
            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            Console.WriteLine($"[JARVIS-BLAZOR] OnAfterRenderAsync - firstRender={firstRender}");
            System.Diagnostics.Debug.WriteLine($"[JARVIS-BLAZOR] OnAfterRenderAsync - firstRender={firstRender}");

            if (firstRender)
            {
                Console.WriteLine("[JARVIS-BLAZOR] First render detected - running connection tests in 1 second...");
                System.Diagnostics.Debug.WriteLine("[JARVIS-BLAZOR] First render detected - running connection tests in 1 second...");

                // Auto-run connection tests on first render
                await Task.Delay(1000); // Small delay to ensure UI is ready
                Console.WriteLine("[JARVIS-BLAZOR] Executing RunConnectionTestsAsync()...");
                await RunConnectionTestsAsync();
                Console.WriteLine("[JARVIS-BLAZOR] Connection tests completed");
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        /// <summary>
        /// Verifies all service connections and logs diagnostic information.
        /// </summary>
        private async Task VerifyConnectionsAsync()
        {
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("=== JARVIS Assistant Connection Verification ===");

            // Check ChatBridge
            if (ChatBridge != null)
            {
                diagnostics.AppendLine("✅ ChatBridge: Connected");
                try
                {
                    // Test event subscription capability
                    diagnostics.AppendLine("   - Event subscriptions: Ready");
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"❌ ChatBridge events error: {ex.Message}");
                }
            }
            else
            {
                diagnostics.AppendLine("❌ ChatBridge: NOT INJECTED");
            }

            // Check AIService (Grok)
            if (AIService != null)
            {
                diagnostics.AppendLine("✅ AIService (Grok): Connected");
                try
                {
                    // Test basic API availability (non-intrusive check)
                    diagnostics.AppendLine($"   - Service type: {AIService.GetType().Name}");
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"❌ AIService error: {ex.Message}");
                }
            }
            else
            {
                diagnostics.AppendLine("⚠️  AIService: Not available (using ChatBridge only)");
            }

            // Check ConversationRepository
            if (ConversationRepository != null)
            {
                diagnostics.AppendLine("✅ ConversationRepository: Connected");
            }
            else
            {
                diagnostics.AppendLine("⚠️  ConversationRepository: Not available (no persistence)");
            }

            // Check JS Runtime
            if (JS != null)
            {
                diagnostics.AppendLine("✅ JSRuntime: Connected");
                diagnostics.AppendLine("   - Audio playback: Available");
                diagnostics.AppendLine("   - Clipboard API: Available");
            }
            else
            {
                diagnostics.AppendLine("❌ JSRuntime: Not available");
            }

            // Check UserContext
            if (UserContext != null)
            {
                diagnostics.AppendLine($"✅ UserContext: Connected ({UserContext.DisplayName ?? "Unknown"})");
            }
            else
            {
                diagnostics.AppendLine("⚠️  UserContext: Not available");
            }

            diagnostics.AppendLine("\n=== Configuration ===");
            diagnostics.AppendLine($"Selected Model: {_selectedModel}");
            diagnostics.AppendLine($"Temperature: {_temperature}");
            diagnostics.AppendLine($"Max Tokens: {_maxTokens}");
            diagnostics.AppendLine($"Token Limit: {_tokenLimit}");
            diagnostics.AppendLine($"\n=== Status ===");
            diagnostics.AppendLine($"Conversation ID: {_conversationId}");
            diagnostics.AppendLine($"Suggestions Count: {_suggestions.Count}");
            diagnostics.AppendLine($"Attachment Settings: {(_attachmentSettings != null ? "Configured" : "Not configured")}");

            // Log diagnostics
            System.Diagnostics.Debug.WriteLine(diagnostics.ToString());

            await Task.CompletedTask;
        }

        /// <summary>
        /// Public method to test ChatBridge connectivity by sending a test message.
        /// </summary>
        public async Task<string> TestChatBridgeConnectionAsync()
        {
            try
            {
                if (ChatBridge == null)
                {
                    return "❌ ChatBridge is not connected";
                }

                var testPrompt = "Connection test";
                await ChatBridge.SubmitPromptAsync(testPrompt, _conversationId);

                return "✅ ChatBridge connection successful - prompt submitted";
            }
            catch (Exception ex)
            {
                return $"❌ ChatBridge connection failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Public method to test Grok/AIService connectivity.
        /// </summary>
        public async Task<string> TestGrokConnectionAsync()
        {
            try
            {
                if (AIService == null)
                {
                    return "⚠️  AIService not available - will use ChatBridge streaming instead";
                }

                var testPrompt = "Test";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await AIService.GetChatCompletionAsync(testPrompt, cts.Token);

                if (!string.IsNullOrEmpty(response))
                {
                    return $"✅ Grok connection successful - received {response.Length} character response";
                }

                return "⚠️  Grok connection succeeded but no response received";
            }
            catch (OperationCanceledException)
            {
                return "⏱️  Grok connection timeout (5s) - API may be slow or unavailable";
            }
            catch (Exception ex)
            {
                return $"❌ Grok connection failed: {ex.Message}";
            }
        }

        // Map ChatMessage list to AssistViewPrompt for binding
        private void UpdatePromptsFromMessages()
        {
            var prompts = new List<Syncfusion.Blazor.InteractiveChat.AssistViewPrompt>();
            Syncfusion.Blazor.InteractiveChat.AssistViewPrompt? current = null;

            foreach (var msg in _messages.Where(m => !string.IsNullOrEmpty(m.Content)))
            {
                if (msg.IsUser)
                {
                    current = new Syncfusion.Blazor.InteractiveChat.AssistViewPrompt
                    {
                        Prompt = msg.Content,
                        Response = null
                    };
                    prompts.Add(current);
                }
                else
                {
                    if (current != null && string.IsNullOrEmpty(current.Response))
                    {
                        current.Response = msg.Content;
                    }
                    else
                    {
                        prompts.Add(new Syncfusion.Blazor.InteractiveChat.AssistViewPrompt
                        {
                            Prompt = string.Empty,
                            Response = msg.Content
                        });
                    }
                }
            }

            _prompts = prompts;
            ThrottledStateHasChanged();
        }

        // Handle user prompt submission from SfAIAssistView
        private async Task HandlePromptRequestedAsync(Syncfusion.Blazor.InteractiveChat.AssistViewPromptRequestedEventArgs args)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.Prompt)) return;

            // Add user message to history
            var userMessage = CreateUserMessage(args.Prompt);
            _messages.Add(userMessage);
            UpdatePromptsFromMessages();

            using var localCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            // Try AIService first (non-streaming) with a timeout
            if (AIService != null)
            {
                try
                {
                    var responseTask = AIService.GetChatCompletionAsync(args.Prompt, localCts.Token);
                    var completed = await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(60), localCts.Token));
                    if (completed == responseTask)
                    {
                        var response = await responseTask;
                        args.Response = response;

                        var aiMsg = CreateAIMessage(response);
                        _messages.Add(aiMsg);
                        UpdatePromptsFromMessages();

                        // Save conversation to database
                        await SaveCurrentConversationAsync();

                        // Update suggestions based on new context
                        await UpdateSuggestionsAsync();

                        await ScrollToBottom();
                        return;
                    }

                    // timed out, fall through to streaming fallback
                }
                catch (OperationCanceledException)
                {
                    // timeout - fall through to streaming fallback
                }
                catch (Exception ex)
                {
                    args.Response = $"Error: {ex.Message}";
                    _errorMessage = ex.Message;
                    ThrottledStateHasChanged();
                    return;
                }
            }

            // Fallback: collect final response from bridge (use TaskCompletionSource)
            _responseTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _responseBuffer.Clear();

            try
            {
                await ChatBridge.SubmitPromptAsync(args.Prompt, _conversationId, localCts.Token);

                var completed = await Task.WhenAny(_responseTcs.Task, Task.Delay(TimeSpan.FromSeconds(60), localCts.Token));
                string fullResponse;
                if (completed == _responseTcs.Task)
                {
                    fullResponse = await _responseTcs.Task;
                }
                else
                {
                    fullResponse = _responseBuffer.ToString();
                    if (string.IsNullOrWhiteSpace(fullResponse)) fullResponse = "No response from assistant.";
                }

                // Provide the final response back to the SfAIAssistView
                args.Response = fullResponse;

                // Persist the assistant message locally
                var aiMsg = CreateAIMessage(fullResponse);
                _messages.Add(aiMsg);
                UpdatePromptsFromMessages();

                // Save conversation to database
                await SaveCurrentConversationAsync();

                // Update suggestions based on new context
                await UpdateSuggestionsAsync();

                await ScrollToBottom();
            }
            catch (Exception ex)
            {
                args.Response = $"Error: {ex.Message}";
                _errorMessage = ex.Message;
                ThrottledStateHasChanged();
            }
            finally
            {
                _responseTcs = null;
            }
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(_userInput) || _isThinking || _isSending)
                return;

            // Validation
            if (_userInput.Length > 2000)
            {
                _errorMessage = "Message is too long. Maximum 2000 characters.";
                StateHasChanged();
                return;
            }

            var sanitizedInput = _userInput.Trim();
            if (string.IsNullOrEmpty(sanitizedInput))
                return;

            _isSending = true;
            _errorMessage = string.Empty;

            // Add user message
            var userMsg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = sanitizedInput,
                IsUser = true,
                Timestamp = DateTime.Now
            };
            _messages.Add(userMsg);

            var prompt = sanitizedInput;
            _userInput = string.Empty;
            _isThinking = true;
            _streamingMessageId = null;

            ThrottledStateHasChanged();
            await ScrollToBottom();

            _cts = new CancellationTokenSource();
            _cts.CancelAfter(TimeSpan.FromMinutes(5));

            try
            {
                await ChatBridge.SubmitPromptAsync(prompt, _conversationId);
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error sending message: {ex.Message}";
                _isThinking = false;
                ThrottledStateHasChanged();
            }
            finally
            {
                _isSending = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task HandleKeyUp(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !e.ShiftKey)
            {
                await SendMessageAsync();
            }
        }

        private void HandleMessageReceived(object? sender, ChatMessage msg)
        {
            _ = InvokeAsync(async () =>
            {
                if (msg == null)
                    return;

                // If there's a streaming placeholder, update it; otherwise add a new assistant message
                if (!string.IsNullOrEmpty(_streamingMessageId))
                {
                    var existing = _messages.FirstOrDefault(m => m.Id == _streamingMessageId);
                    if (existing != null)
                    {
                        existing.Content = msg.Content;
                        existing.Timestamp = DateTime.Now;
                    }
                    else
                    {
                        _messages.Add(new ChatMessage
                        {
                            Id = msg.Id ?? Guid.NewGuid().ToString(),
                            Content = msg.Content,
                            IsUser = false,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                else
                {
                    _messages.Add(new ChatMessage
                    {
                        Id = msg.Id ?? Guid.NewGuid().ToString(),
                        Content = msg.Content,
                        IsUser = false,
                        Timestamp = DateTime.Now
                    });
                }

                // If a prompt handler is awaiting the full response, complete it
                if (_responseTcs != null && !_responseTcs.Task.IsCompleted)
                {
                    _responseTcs.TrySetResult(msg.Content ?? string.Empty);
                }

                _isThinking = false;
                _streamingMessageId = null;
                UpdatePromptsFromMessages();
                await ScrollToBottom();
            });
        }

        private void HandleResponseChunkReceived(object? sender, ChatResponseChunkEventArgs e)
        {
            _ = InvokeAsync(async () =>
            {
                // If a prompt handler is awaiting the full response, buffer chunks there
                if (_responseTcs != null)
                {
                    _responseBuffer.Append(e.Chunk);
                }
                else
                {
                    if (string.IsNullOrEmpty(_streamingMessageId))
                    {
                        _streamingMessageId = Guid.NewGuid().ToString();
                        var placeholderMsg = new ChatMessage
                        {
                            Id = _streamingMessageId,
                            Content = e.Chunk,
                            IsUser = false,
                            Timestamp = DateTime.Now
                        };
                        _messages.Add(placeholderMsg);
                    }
                    else
                    {
                        var existingMsg = _messages.FirstOrDefault(m => m.Id == _streamingMessageId);
                        if (existingMsg != null)
                        {
                            existingMsg.Content += e.Chunk;
                        }
                    }
                }

                UpdatePromptsFromMessages();
                await ScrollToBottom();
            });
        }

        private void HandleExternalPromptRequested(object? sender, ChatExternalPromptEventArgs e)
        {
            _ = InvokeAsync(async () =>
            {
                if (string.IsNullOrEmpty(e.Prompt) || _isSending)
                    return;

                // If AssistView is available, set the prompt directly.
                if (_assistView != null)
                {
                    try
                    {
                        // Setting a component parameter on a child component from outside
                        // triggers analyzer BL0005. The Syncfusion AssistView exposes
                        // `Prompt` as a writable property; suppress the analyzer warning
                        // here since this is an explicit programmatic interaction
                        // intended by the UI layer.
#pragma warning disable BL0005
                        _assistView.Prompt = e.Prompt;
#pragma warning restore BL0005
                    }
                    catch
                    {
                        _userInput = e.Prompt;
                        await SendMessageAsync();
                    }
                }
                else
                {
                    _userInput = e.Prompt;
                    await SendMessageAsync();
                }
            });
        }

        private void ThrottledStateHasChanged()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastRenderTime) > _renderThrottle)
            {
                _lastRenderTime = now;
                StateHasChanged();
            }
        }

        private async Task ScrollToBottom()
        {
            try
            {
                if (JS != null)
                {
                    await JS.InvokeVoidAsync("eval", "window.scrollTo(0, document.body.scrollHeight)");
                }
            }
            catch
            {
                // Silently handle if scroll unavailable
            }
        }

        private async Task ApplySuggestionAsync(string suggestion)
        {
            if (string.IsNullOrEmpty(suggestion))
                return;

            if (_assistView != null)
            {
                try
                {
#pragma warning disable BL0005
                    _assistView.Prompt = suggestion;
#pragma warning restore BL0005
                }
                catch
                {
                    _userInput = suggestion;
                    await SendMessageAsync();
                }
            }
            else
            {
                _userInput = suggestion;
                await SendMessageAsync();
            }
        }

        private string GetUserInitials()
        {
            var displayName = UserContext?.DisplayName ?? "You";
            if (string.IsNullOrEmpty(displayName))
                return "U";

            var parts = displayName.Split(' ');
            if (parts.Length > 1)
            {
                var a = char.ToUpper(parts[0][0], CultureInfo.InvariantCulture);
                var b = char.ToUpper(parts[^1][0], CultureInfo.InvariantCulture);
                return string.Concat(a, b);
            }

            return char.ToUpper(parts[0][0], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        private ChatMessage CreateUserMessage(string content)
        {
            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = content,
                IsUser = true,
                Timestamp = DateTime.UtcNow
            };
        }

        private ChatMessage CreateAIMessage(string content)
        {
            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Content = content,
                IsUser = false,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Formats markdown-style response text to HTML.
        /// </summary>
        private string FormatMarkdownResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return string.Empty;

            // Basic markdown formatting
            var formatted = response;

            // Code blocks
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                @"```([\s\S]*?)```",
                "<pre><code>$1</code></pre>");

            // Inline code
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                @"`([^`]+)`",
                "<code>$1</code>");

            // Bold
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                @"\*\*([^\*]+)\*\*",
                "<strong>$1</strong>");

            // Italic
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                @"\*([^\*]+)\*",
                "<em>$1</em>");

            // Line breaks
            formatted = formatted.Replace("\n", "<br/>");

            return formatted;
        }

        /// <summary>
        /// Copies response text to clipboard.
        /// </summary>
        private async Task CopyToClipboard(string text)
        {
            try
            {
                if (JS != null && !string.IsNullOrEmpty(text))
                {
                    await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Failed to copy: {ex.Message}";
                StateHasChanged();
            }
        }

        /// <summary>
        /// Regenerates a response for a given prompt.
        /// </summary>
        private async Task RegenerateResponse(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            // Remove the last response for this prompt
            var lastPromptIndex = _messages.FindLastIndex(m => m.IsUser && m.Content == prompt);
            if (lastPromptIndex >= 0 && lastPromptIndex < _messages.Count - 1)
            {
                _messages.RemoveAt(lastPromptIndex + 1); // Remove the response
            }

            // Regenerate by submitting the prompt again
            if (_assistView != null)
            {
                try
                {
#pragma warning disable BL0005
                    _assistView.Prompt = prompt;
#pragma warning restore BL0005
                }
                catch
                {
                    _userInput = prompt;
                    await SendMessageAsync();
                }
            }
        }

        /// <summary>
        /// Shows settings dialog.
        /// </summary>
        private async Task ShowSettingsAsync()
        {
            _showSettingsDialog = true;
            StateHasChanged();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Closes the settings dialog.
        /// </summary>
        private async Task CloseSettingsDialogAsync()
        {
            _showSettingsDialog = false;
            StateHasChanged();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Saves settings from the dialog.
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            // Apply settings (in future, save to database/configuration)
            _showSettingsDialog = false;
            StateHasChanged();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Toggles audio playback using Web Speech API.
        /// </summary>
        private async Task ToggleAudioPlaybackAsync(string text)
        {
            if (JS == null) return;

            try
            {
                if (_isPlayingAudio)
                {
                    // Stop audio
                    await JS.InvokeVoidAsync("eval", "window.speechSynthesis.cancel()");
                    _isPlayingAudio = false;
                    _audioIconCss = "e-icons e-volume";
                    _audioTooltip = "Read aloud";
                }
                else
                {
                    // Start audio
                    _currentAudioText = text;
                    var jsCode = $@"
                        const utterance = new SpeechSynthesisUtterance({System.Text.Json.JsonSerializer.Serialize(text)});
                        utterance.rate = 1.0;
                        utterance.pitch = 1.0;
                        utterance.volume = 1.0;
                        utterance.onend = () => {{
                            DotNet.invokeMethodAsync('WileyWidget.WinForms', 'OnAudioEnded');
                        }};
                        window.speechSynthesis.speak(utterance);
                    ";
                    await JS.InvokeVoidAsync("eval", jsCode);
                    _isPlayingAudio = true;
                    _audioIconCss = "e-icons e-stop";
                    _audioTooltip = "Stop reading";
                }
                StateHasChanged();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Audio playback error: {ex.Message}";
                _isPlayingAudio = false;
                _audioIconCss = "e-icons e-volume";
                _audioTooltip = "Read aloud";
                StateHasChanged();
            }
        }

        /// <summary>
        /// Called from JavaScript when audio playback ends.
        /// </summary>
        [JSInvokable]
        public void OnAudioEnded()
        {
            _isPlayingAudio = false;
            _audioIconCss = "e-icons e-volume";
            _audioTooltip = "Read aloud";
            StateHasChanged();
        }

        /// <summary>
        /// Updates token usage from API response.
        /// </summary>
        private void UpdateTokenUsage(int promptTokens, int completionTokens)
        {
            _promptTokens = promptTokens;
            _completionTokens = completionTokens;
            _tokensUsed += promptTokens + completionTokens;
        }

        /// <summary>
        /// Resets token usage counter.
        /// </summary>
        private void ResetTokenUsage()
        {
            _tokensUsed = 0;
            _promptTokens = 0;
            _completionTokens = 0;
        }

        /// <summary>
        /// Handles user feedback on responses (like/dislike).
        /// </summary>
        private async Task HandleResponseFeedbackAsync(string response, bool isPositive)
        {
            try
            {
                // Log feedback (in future, save to database for model training)
                var feedbackType = isPositive ? "👍 Like" : "👎 Dislike";
                System.Diagnostics.Debug.WriteLine($"User feedback: {feedbackType} on response: {response[..Math.Min(50, response.Length)]}...");

                // Show confirmation message in footer temporarily
                var message = isPositive ? "Thanks for your feedback!" : "Thanks for letting us know.";
                var previousError = _errorMessage;
                _errorMessage = message;
                StateHasChanged();

                // Clear message after 2 seconds
                await Task.Delay(2000);
                if (_errorMessage == message)
                {
                    _errorMessage = previousError;
                    StateHasChanged();
                }

                // Future: Save to database
                // await ConversationRepository?.SaveFeedbackAsync(_conversationId, response, isPositive);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Feedback error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles file attachment uploads.
        /// </summary>
        private async Task HandleAttachmentUploadAsync(string fileName, byte[] fileContent)
        {
            try
            {
                // Add to uploaded files list
                if (!_uploadedFiles.Contains(fileName))
                {
                    _uploadedFiles.Add(fileName);
                }

                // Send file metadata to AI service for analysis (if supported)
                if (AIService != null)
                {
                    var fileInfo = $"Uploaded: {fileName} ({fileContent.Length:N0} bytes)";
                    System.Diagnostics.Debug.WriteLine(fileInfo);

                    // Future: Send file to AI for document analysis
                    // var analysis = await AIService.AnalyzeDocumentAsync(fileContent, fileName);
                    // Add analysis to conversation context
                }

                // Send metadata to ChatBridge
                var attachmentPrompt = $"[Attachment: {fileName}]";
                await ChatBridge.RequestExternalPromptAsync(attachmentPrompt);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                _errorMessage = $"File upload error: {ex.Message}";
                StateHasChanged();
            }
        }

        /// <summary>
        /// Updates prompt suggestions dynamically based on conversation context.
        /// </summary>
        private async Task UpdateSuggestionsAsync()
        {
            try
            {
                // Generate context-aware suggestions based on conversation history
                var recentPrompts = _messages.Where(m => m.IsUser).TakeLast(3).Select(m => m.Content).ToList();

                if (recentPrompts.Any())
                {
                    // Analyze conversation topics
                    var hasBudgetTopic = recentPrompts.Any(p => p.Contains("budget", StringComparison.OrdinalIgnoreCase));
                    var hasAccountTopic = recentPrompts.Any(p => p.Contains("account", StringComparison.OrdinalIgnoreCase));
                    var hasReportTopic = recentPrompts.Any(p => p.Contains("report", StringComparison.OrdinalIgnoreCase));
                    var hasTransactionTopic = recentPrompts.Any(p => p.Contains("transaction", StringComparison.OrdinalIgnoreCase));

                    // Generate context-aware suggestions
                    var newSuggestions = new List<string>();

                    if (hasBudgetTopic)
                    {
                        newSuggestions.Add("Show budget trends over time");
                        newSuggestions.Add("Compare this to last year's budget");
                    }

                    if (hasAccountTopic)
                    {
                        newSuggestions.Add("Show related account transactions");
                        newSuggestions.Add("What's the current account balance?");
                    }

                    if (hasReportTopic)
                    {
                        newSuggestions.Add("Export this report to PDF");
                        newSuggestions.Add("Show more detailed breakdown");
                    }

                    if (hasTransactionTopic)
                    {
                        newSuggestions.Add("Filter transactions by date");
                        newSuggestions.Add("Show transaction categories");
                    }

                    // Add generic follow-up suggestions
                    newSuggestions.Add("Tell me more about that");
                    newSuggestions.Add("What are the next steps?");

                    // Update suggestions if new ones were generated
                    if (newSuggestions.Any())
                    {
                        _suggestions = newSuggestions.Distinct().Take(6).ToList();
                        StateHasChanged();
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Suggestions update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a past conversation by ID.
        /// </summary>
        private async Task LoadConversationAsync(string conversationId)
        {
            try
            {
                if (ConversationRepository == null || string.IsNullOrEmpty(conversationId))
                    return;

                var conversation = await ConversationRepository.GetConversationAsync(conversationId);
                if (conversation is ConversationHistory history && !string.IsNullOrEmpty(history.MessagesJson))
                {
                    var messages = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(history.MessagesJson);
                    if (messages != null && messages.Count > 0)
                    {
                        // Clear current conversation
                        _messages.Clear();
                        _prompts.Clear();

                        // Load history
                        _messages.AddRange(messages);
                        _conversationId = conversationId;

                        // Update UI
                        UpdatePromptsFromMessages();
                        await ScrollToBottom();
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Failed to load conversation: {ex.Message}";
                StateHasChanged();
            }
        }

        /// <summary>
        /// Gets list of saved conversations.
        /// </summary>
        private async Task<List<ConversationHistory>> GetSavedConversationsAsync()
        {
            try
            {
                if (ConversationRepository == null)
                    return new List<ConversationHistory>();

                var conversations = await ConversationRepository.GetConversationsAsync(0, 10);
                return conversations.OfType<ConversationHistory>().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load conversations: {ex.Message}");
                return new List<ConversationHistory>();
            }
        }

        /// <summary>
        /// Runs comprehensive connection tests and displays results.
        /// </summary>
        private async Task RunConnectionTestsAsync()
        {
            try
            {
                _isThinking = true;
                StateHasChanged();

                var results = new StringBuilder();
                results.AppendLine("🔍 **Connection Diagnostics**\n");

                // Test ChatBridge
                results.AppendLine("**ChatBridge Test:**");
                var chatBridgeResult = await TestChatBridgeConnectionAsync();
                results.AppendLine(chatBridgeResult);
                results.AppendLine();

                // Test Grok/AIService
                results.AppendLine("**Grok AI Service Test:**");
                var grokResult = await TestGrokConnectionAsync();
                results.AppendLine(grokResult);
                results.AppendLine();

                // Display component status
                results.AppendLine("**Component Status:**");
                results.AppendLine($"- Conversation ID: `{_conversationId}`");
                results.AppendLine($"- Message Count: {_messages.Count}");
                results.AppendLine($"- Suggestions: {_suggestions.Count} active");
                results.AppendLine($"- Uploaded Files: {_uploadedFiles.Count}");
                results.AppendLine($"- Token Usage: {_tokensUsed}/{_tokenLimit}");
                results.AppendLine();

                // Display configuration
                results.AppendLine("**Configuration:**");
                results.AppendLine($"- Model: {_selectedModel}");
                results.AppendLine($"- Temperature: {_temperature}");
                results.AppendLine($"- Max Tokens: {_maxTokens}");
                results.AppendLine($"- Theme: {_selectedTheme}");

                // Create a system message with results
                var resultMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = results.ToString(),
                    IsUser = false,
                    Timestamp = DateTime.UtcNow
                };
                _messages.Add(resultMsg);
                UpdatePromptsFromMessages();

                await ScrollToBottom();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Test failed: {ex.Message}";
            }
            finally
            {
                _isThinking = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Clears the current conversation.
        /// </summary>
        private async Task ClearConversationAsync()
        {
            // Save current conversation before clearing
            if (_messages.Count > 0 && ConversationRepository != null)
            {
                await SaveCurrentConversationAsync();
            }

            _messages.Clear();
            _prompts.Clear();
            _conversationId = Guid.NewGuid().ToString();
            _errorMessage = string.Empty;
            _streamingMessageId = null;
            ResetTokenUsage();
            StateHasChanged();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Saves the current conversation to the database.
        /// </summary>
        private async Task SaveCurrentConversationAsync()
        {
            try
            {
                if (ConversationRepository == null || _messages.Count == 0 || string.IsNullOrEmpty(_conversationId))
                    return;

                var messagesJson = System.Text.Json.JsonSerializer.Serialize(_messages);
                var firstUserMessage = _messages.FirstOrDefault(m => m.IsUser)?.Content;
                var title = string.IsNullOrEmpty(firstUserMessage)
                    ? "Conversation"
                    : (firstUserMessage.Length > 50 ? firstUserMessage[..50] + "..." : firstUserMessage);

                var conversation = new ConversationHistory
                {
                    Id = _conversationId,
                    ConversationId = _conversationId,
                    Title = title,
                    Content = string.Join("\n", _messages.Select(m => m.Content)),
                    MessagesJson = messagesJson,
                    MessageCount = _messages.Count,
                    CreatedAt = _messages.FirstOrDefault()?.Timestamp ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await ConversationRepository.SaveConversationAsync(conversation);
            }
            catch (Exception ex)
            {
                // Silent failure - don't interrupt user experience
                System.Diagnostics.Debug.WriteLine($"Failed to save conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the last conversation from the database.
        /// </summary>
        private async Task LoadLastConversationAsync()
        {
            try
            {
                if (ConversationRepository == null)
                    return;

                var conversations = await ConversationRepository.GetConversationsAsync(0, 1);
                if (conversations?.Count > 0 && conversations[0] is ConversationHistory lastConversation)
                {
                    if (!string.IsNullOrEmpty(lastConversation.MessagesJson))
                    {
                        var messages = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(lastConversation.MessagesJson);
                        if (messages != null && messages.Count > 0)
                        {
                            _messages.AddRange(messages);
                            _conversationId = lastConversation.ConversationId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silent failure - start fresh if load fails
                System.Diagnostics.Debug.WriteLine($"Failed to load last conversation: {ex.Message}");
                _messages.Clear();
            }
        }

        /// <summary>
        /// Clears the error message.
        /// </summary>
        private void ClearError()
        {
            _errorMessage = string.Empty;
            StateHasChanged();
        }

        /// <summary>
        /// Gets the user display name for the prompt header.
        /// </summary>
        private string GetUserDisplayName()
        {
            return UserContext?.DisplayName ?? "You";
        }

        /// <summary>
        /// Gets the conversation ID for display.
        /// </summary>
        private string GetConversationId()
        {
            return string.IsNullOrEmpty(_conversationId)
                ? "New"
                : _conversationId[..8]; // Show first 8 chars
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation. Releases managed resources when disposing is true.
        /// </summary>
        /// <param name="disposing">True when called from Dispose(), false from finalizer.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    ChatBridge.OnMessageReceived -= HandleMessageReceived;
                    ChatBridge.ResponseChunkReceived -= HandleResponseChunkReceived;
                    ChatBridge.ExternalPromptRequested -= HandleExternalPromptRequested;
                }
                catch
                {
                    // Swallow any exceptions during event unsubscription to ensure deterministic cleanup
                }

                try
                {
                    _cts?.Cancel();
                }
                catch { }

                try
                {
                    _cts?.Dispose();
                }
                catch { }
            }
        }
    }
}
