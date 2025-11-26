using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class AIAssistViewModel : ObservableObject
    {
        private readonly ILogger<AIAssistViewModel> _logger;
        private readonly IAIService? _aiService;
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty]
        private string title = "AI Assistant";

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string userQuery = string.Empty;

        [ObservableProperty]
        private string aiResponse = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ChatMessage> chatHistory = new();

        public AIAssistViewModel(
            ILogger<AIAssistViewModel> logger,
            IAIService? aiService = null)
        {
            _logger = logger;
            _aiService = aiService;

            SendQueryCommand = new AsyncRelayCommand(SendQueryAsync, CanSendQuery);
            ClearChatCommand = new AsyncRelayCommand(ClearChatAsync);
        }

        public IAsyncRelayCommand SendQueryCommand { get; }
        public IAsyncRelayCommand ClearChatCommand { get; }

        private bool CanSendQuery()
        {
            return !string.IsNullOrWhiteSpace(UserQuery) && !IsLoading;
        }

        private async Task SendQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(UserQuery)) return;

            try
            {
                IsLoading = true;
                var query = UserQuery;
                UserQuery = string.Empty; // Clear input

                // Add user message to chat
                ChatHistory.Add(new ChatMessage
                {
                    Content = query ?? string.Empty,
                    IsUserMessage = true,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("Processing AI query: {Query}", query);

                // Call actual AI service
                string aiResponseText;
                if (_aiService != null)
                {
                    try
                    {
                        aiResponseText = await _aiService.GetInsightsAsync(
                            "Wiley Widget AI Assistant",
                            query!,
                            _cts.Token);
                        _logger.LogInformation("AI response received: {Length} characters", aiResponseText?.Length ?? 0);
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogWarning(aiEx, "AI service call failed, using fallback");
                        aiResponseText = "I'm currently experiencing technical difficulties. Please try again later or rephrase your question.";
                    }
                }
                else
                {
                    aiResponseText = "AI service is not configured. Please configure the AI API key to enable AI features.";
                    _logger.LogWarning("AI service not available - returning configuration message");
                }

                AiResponse = aiResponseText!;

                // Add AI response to chat
                ChatHistory.Add(new ChatMessage
                {
                    Content = AiResponse ?? string.Empty,
                    IsUserMessage = false,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("AI response generated and added to chat");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process AI query");
                AiResponse = "Sorry, I encountered an error processing your request.";
            }
            finally
            {
                IsLoading = false;
                SendQueryCommand.NotifyCanExecuteChanged();
            }
        }

        private Task ClearChatAsync()
        {
            ChatHistory.Clear();
            AiResponse = string.Empty;
            _logger.LogInformation("Chat history cleared");
            return Task.CompletedTask;
        }
    }

    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUserMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}