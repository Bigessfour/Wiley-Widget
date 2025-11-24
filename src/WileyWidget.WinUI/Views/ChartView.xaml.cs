using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.Logging;
using WileyWidget.WinUI.ViewModels;
using System;

namespace WileyWidget.WinUI.Views
{
    public sealed partial class ChartView : Page
    {
        private readonly ILogger<ChartView> _logger;

        public ChartViewModel ViewModel { get; }

        public ChartView()
        {
            this.InitializeComponent();

            _logger = App.Services?.GetService(typeof(ILogger<ChartView>)) as ILogger<ChartView>
                ?? throw new InvalidOperationException("Logger not available");

            ViewModel = App.Services?.GetService(typeof(ChartViewModel)) as ChartViewModel
                ?? new ChartViewModel(
                    App.Services?.GetService(typeof(ILogger<ChartViewModel>)) as ILogger<ChartViewModel>);

            this.DataContext = ViewModel;

            _logger.LogInformation("ChartView initialized");

            this.Loaded += ChartView_Loaded;
        }

        private async void ChartView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _logger.LogInformation("ChartView loaded, initializing chart data");
            await ViewModel.LoadChartDataAsync();
        }
    }
}
