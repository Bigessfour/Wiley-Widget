using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinUI.ViewModels;
using System;

namespace WileyWidget.WinUI.Views
{
    public sealed partial class DataView : Page
    {
        private readonly ILogger<DataView> _logger;

        public DataViewModel ViewModel { get; }

        public DataView()
        {
            this.InitializeComponent();

            // Resolve dependencies from DI container
            _logger = App.Services?.GetRequiredService<ILogger<DataView>>()
                ?? throw new InvalidOperationException("Logger not available from DI container");

            ViewModel = App.Services?.GetRequiredService<DataViewModel>()
                ?? throw new InvalidOperationException("DataViewModel not available from DI container");

            // Set DataContext for data binding
            this.DataContext = ViewModel;

            _logger.LogInformation("DataView initialized with DataContext");

            this.Loaded += DataView_Loaded;
        }

        private async void DataView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _logger.LogInformation("DataView loaded, initializing data");
            await ViewModel.LoadDataAsync();
        }
    }
}
