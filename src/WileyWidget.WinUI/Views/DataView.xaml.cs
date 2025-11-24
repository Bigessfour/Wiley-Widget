using Microsoft.UI.Xaml.Controls;
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

            _logger = App.Services?.GetService(typeof(ILogger<DataView>)) as ILogger<DataView>
                ?? throw new InvalidOperationException("Logger not available");

            ViewModel = App.Services?.GetService(typeof(DataViewModel)) as DataViewModel
                ?? new DataViewModel(
                    App.Services?.GetService(typeof(ILogger<DataViewModel>)) as ILogger<DataViewModel>);

            this.DataContext = ViewModel;

            _logger.LogInformation("DataView initialized");

            this.Loaded += DataView_Loaded;
        }

        private async void DataView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _logger.LogInformation("DataView loaded, initializing data");
            await ViewModel.LoadDataAsync();
        }
    }
}
