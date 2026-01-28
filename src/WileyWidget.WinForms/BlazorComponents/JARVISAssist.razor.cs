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

        // AssistView backing state
        private Syncfusion.Blazor.InteractiveChat.SfAIAssistView? _assistView;
        private List<Syncfusion.Blazor.InteractiveChat.AssistViewPrompt> _prompts = new();
        private List<string> _suggestions = new() { "Are you connected?", "Tell me about Wiley Widget" };
        private TaskCompletionSource<string>? _responseTcs;
        private readonly StringBuilder _responseBuffer = new();
        [Inject]
        private IAIService? AIService { get; set; }
        [Inject]
        private IJSRuntime? JS { get; set; }
        [CascadingParameter]
        private WileyWidget.Services.Abstractions.IUserContext? UserContext { get; set; }

        protected override async Task OnInitializedAsync()
        {
            ChatBridge.OnMessageReceived += HandleMessageReceived;
            ChatBridge.ResponseChunkReceived += HandleResponseChunkReceived;
            ChatBridge.ExternalPromptRequested += HandleExternalPromptRequested;
            _conversationId = Guid.NewGuid().ToString();

            // Map any existing messages into the AssistView prompts
            UpdatePromptsFromMessages();

            await base.OnInitializedAsync();
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
