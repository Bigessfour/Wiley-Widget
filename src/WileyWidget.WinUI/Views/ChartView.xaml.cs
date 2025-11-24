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
            // Ensure the partial class is generated from ChartView.xaml
            this.InitializeComponent();

            // Resolve dependencies from DI container, fallback to safe defaults if DI fails
            try
            {
                _logger = App.Services?.GetService<ILogger<ChartView>>();
            }
            catch
            {
                _logger = null!;
            }

            ViewModel = App.Services?.GetService<ChartViewModel>();
            if (ViewModel == null)
            {
                try { _logger?.LogError("ChartViewModel not available from DI container; using fallback instance"); } catch { }
                ViewModel = new ChartViewModel();
            }

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
