using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.UI.Xaml.TreeGrid;
using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget.Views.Main;

/// <summary>
/// GASB-Compliant Municipal Budget Management UserControl
/// Provides hierarchical budget account management with Excel import/export
/// </summary>
public partial class BudgetView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetView"/> class.
    /// ViewModel is auto-wired by Prism ViewModelLocator.
    /// </summary>
    public BudgetView()
    {
        InitializeComponent();
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
