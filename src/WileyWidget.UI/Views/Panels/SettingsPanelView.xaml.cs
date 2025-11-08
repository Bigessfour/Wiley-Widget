using System.Windows.Controls;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Panels;

/// <summary>
/// Settings panel view for embedding in docking layout
/// </summary>
public partial class SettingsPanelView : UserControl
{
    private bool _loadedOnce;

    public SettingsPanelView()
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator

        // Load settings when control loads
        Loaded += SettingsPanelView_Loaded;
    }

    private async void SettingsPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_loadedOnce) return;
        _loadedOnce = true;

        try
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                await viewModel.LoadSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "SettingsPanelView: Failed to load settings on first load");
        }
    }
}
