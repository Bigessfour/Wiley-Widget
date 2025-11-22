using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinUI.ViewModels.Main
{
    public partial class AIAssistViewModel : ObservableRecipient
    {
        private readonly ILogger<AIAssistViewModel> _logger;

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

        public AIAssistViewModel(ILogger<AIAssistViewModel> logger)
        {
            _logger = logger;

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
                    Content = query,
                    IsUserMessage = true,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("Processing AI query: {Query}", query);

                // Placeholder for AI processing logic
                AiResponse = $"Processing your query: {query}";

                // Add AI response to chat
                ChatHistory.Add(new ChatMessage
                {
                    Content = AiResponse,
                    IsUserMessage = false,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation("AI response generated");
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

        private async Task ClearChatAsync()
        {
            ChatHistory.Clear();
            AiResponse = string.Empty;
            _logger.LogInformation("Chat history cleared");
        }
    }

    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsUserMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}