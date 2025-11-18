using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        public ICommand TestCommand { get; }

        private async void DoTheThing()
        {
            await _aiLogger.LogAsync("User clicked the magic button");
            _quickBooksService.DoSomething();
            // For demo, just log; in real app, call service methods
            await Task.CompletedTask;
        }
    }
}