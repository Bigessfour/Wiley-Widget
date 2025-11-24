using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
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

            // Resolve dependencies from DI container
            _logger = App.Services?.GetRequiredService<ILogger<ChartView>>()
                ?? throw new InvalidOperationException("Logger not available from DI container");

            ViewModel = App.Services?.GetRequiredService<ChartViewModel>()
                ?? throw new InvalidOperationException("ChartViewModel not available from DI container");

            // Set DataContext for data binding
            this.DataContext = ViewModel;

            _logger.LogInformation("ChartView initialized with DataContext");

            this.Loaded += ChartView_Loaded;
        }

        private async void ChartView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _logger.LogInformation("ChartView loaded, initializing chart data");
            await ViewModel.LoadChartDataAsync();
        }
    }
}
