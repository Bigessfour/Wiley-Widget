using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Microsoft.Extensions.Logging;

namespace WileyWidget.ViewModels.Main
{
    public class AIAssistViewModel : BindableBase, INavigationAware
    {
        private readonly ILogger<AIAssistViewModel> _logger;

        private string _title = "AI Assistant";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _query = string.Empty;
        public string Query
        {
            get => _query;
            set => SetProperty(ref _query, value);
        }

        private ObservableCollection<AIResponse> _responses = new();
        public ObservableCollection<AIResponse> Responses
        {
            get => _responses;
            set => SetProperty(ref _responses, value);
        }

        public AIAssistViewModel(ILogger<AIAssistViewModel> logger)
        {
            _logger = logger;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated to AI Assist View");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("Navigated from AI Assist View");
        }

        public async Task SubmitQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(Query))
                return;

            try
            {
                IsLoading = true;
                _logger.LogInformation("Submitting AI query: {Query}", Query);

                // Add user query to responses
                Responses.Add(new AIResponse
                {
                    IsUserMessage = true,
                    Content = Query,
                    Timestamp = DateTime.Now
                });

                // Simulate AI response (replace with actual AI service call)
                await Task.Delay(1000);

                Responses.Add(new AIResponse
                {
                    IsUserMessage = false,
                    Content = $"AI Response to: {Query}",
                    Timestamp = DateTime.Now
                });

                Query = string.Empty;
                _logger.LogInformation("AI query processed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process AI query");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class AIResponse
    {
        public bool IsUserMessage { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}