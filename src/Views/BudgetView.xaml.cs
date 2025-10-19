using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.TreeGrid;
using Syncfusion.UI.Xaml.Grid;
using Serilog;

namespace WileyWidget;

/// <summary>
/// GASB-Compliant Municipal Budget Management UserControl
/// Provides hierarchical budget account management with Excel import/export
/// </summary>
public partial class BudgetView : UserControl
{
    public BudgetView()
    {
        InitializeBudgetView();
    }

    /// <summary>
    /// Internal constructor for unit testing scenarios with a preconfigured view model.
    /// </summary>
    /// <param name="viewModel">Budget view model to bind to the view.</param>
    public BudgetView(BudgetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeBudgetView();
        DataContext = viewModel;
    }

    /// <summary>
    /// Ensures the XAML content loads even when analyzers cannot discover the generated InitializeComponent.
    /// </summary>
    private void InitializeBudgetView()
    {
        try
        {
            var resourceLocator = new Uri("/WileyWidget;component/src/Views/BudgetView.xaml", UriKind.Relative);
            Application.LoadComponent(this, resourceLocator);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load BudgetView XAML content");
            throw;
        }
    }

    /// <summary>
    /// Handles the Loaded event to refresh budget data
    /// </summary>
    private async void BudgetView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BudgetViewModel vm)
        {
            await vm.RefreshBudgetDataAsync();
        }
    }

    /// <summary>
    /// Handles cell tooltip opening to provide contextual GASB guidance
    /// </summary>
    private void BudgetTreeGrid_CellToolTipOpening(object sender, TreeGridCellToolTipOpeningEventArgs e)
    {
        // Syncfusion TreeGrid tooltips are handled via ToolTip property on columns
        // Additional custom tooltip logic can be added here if needed
        Log.Debug($"Cell tooltip opening for column: {e.Column?.MappingName}");
    }

    /// <summary>
    /// Handles cell edit completion to trigger ViewModel calculations
    /// </summary>
    private void BudgetTreeGrid_CurrentCellEndEdit(object sender, CurrentCellEndEditEventArgs e)
    {
        if (DataContext is BudgetViewModel vm)
        {
            // Recalculate totals when any cell is edited
            vm.RefreshBudgetDataCommand?.Execute();
        }

        Log.Information("Cell edit completed in BudgetTreeGrid");
    }

    /// <summary>
    /// Creates a standalone window hosting the budget view.
    /// </summary>
    public static void ShowBudgetWindow()
    {
        BudgetViewModel? viewModel = null;
        try
        {
            var containerProvider = App.GetContainerProvider();
            viewModel = containerProvider.Resolve<BudgetViewModel>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to resolve BudgetViewModel from DI container");
            viewModel = null;
        }

        if (viewModel == null)
        {
            Log.Information("Falling back to default BudgetView without DI-bound ViewModel");
        }

        var view = viewModel != null ? new BudgetView(viewModel) : new BudgetView();

        var window = new Window
        {
            Title = "Municipal Budget Analysis",
            Content = view,
            Owner = Application.Current?.MainWindow,
            Width = 1280,
            Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        window.Show();
    }

    // Methods for UI test compatibility
    public void Show()
    {
        // UserControl doesn't have Show, but make it visible
        Visibility = Visibility.Visible;
    }

    public void Close()
    {
        // UserControl doesn't have Close, but hide it
        Visibility = Visibility.Collapsed;
    }

    public string Title => "Budget";
}
