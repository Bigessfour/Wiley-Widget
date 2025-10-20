using System;
using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;
using Serilog;

namespace WileyWidget.Views;

/// <summary>
/// Interaction logic for MunicipalAccountView.xaml
/// </summary>
public partial class MunicipalAccountView : UserControl
{
    private bool _loadedOnce;
    /// <summary>
    /// Initializes a new instance of the MunicipalAccountView
    /// </summary>
    public MunicipalAccountView()
    {
        InitializeComponent();

        // ViewModel is auto-wired by Prism ViewModelLocator
        // Load data when control is loaded
        Loaded += MunicipalAccountView_Loaded;
    }

    private async void MunicipalAccountView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
                if (_loadedOnce) return;
                _loadedOnce = true;
                if (DataContext is MunicipalAccountViewModel viewModel)
                {
                    await viewModel.InitializeAsync();
                    Log.Information("MunicipalAccountView data initialized");
                }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MunicipalAccountView data");
            MessageBox.Show(
                $"Failed to load account data: {ex.Message}",
                "Data Loading Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
