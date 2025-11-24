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

            // Resolve dependencies from DI container, fallback to safe defaults if DI fails
            try
            {
                _logger = App.Services?.GetService<ILogger<DataView>>();
            }
            catch
            {
                _logger = null!;
            }

            ViewModel = App.Services?.GetService<DataViewModel>();
            if (ViewModel == null)
            {
                // Log DI resolution failure if logger available, then create a fallback minimal ViewModel
                try { _logger?.LogError("DataViewModel not available from DI container; using fallback instance"); } catch { }
                ViewModel = new DataViewModel();
            }

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
