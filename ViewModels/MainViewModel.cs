using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Input;
using WileyWidget.Services;

namespace WileyWidget.ViewModels
{
    public class MainViewModel : ObservableRecipient
    {
        private readonly AILoggingService _aiLogger;
        private readonly QuickBooksService _quickBooksService;

        public MainViewModel(AILoggingService aiLogger, QuickBooksService quickBooksService)
        {
            _aiLogger = aiLogger;
            _quickBooksService = quickBooksService;
            TestCommand = new RelayCommand(DoTheThing);
        }

        public IRelayCommand TestCommand { get; }

        private async void DoTheThing()
        {
            await _aiLogger.LogAsync("User clicked the magic button");
            _quickBooksService.DoSomething();
            // For demo, just log; in real app, call service methods
            await Task.CompletedTask;
        }
    }
}