using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Syncfusion.Windows.Tools.Controls;
using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Customer Management Window - Provides full CRUD interface for utility customers
/// Prism auto-wires ViewModel via ViewModelLocator.AutoWireViewModel
/// </summary>
public partial class UtilityCustomerView : Window
{
    public UtilityCustomerView()
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

        Loaded += (_, _) =>
        {
            if (DataContext is UtilityCustomerViewModel vm && vm.Customers.Count == 0)
            {
                vm.LoadCustomersCommand.Execute();
            }
        };
    }

    /// <summary>
    /// Show the Customer Management window
    /// </summary>
    public static void ShowCustomerWindow()
    {
        var window = new UtilityCustomerView();
        window.Show();
    }

    /// <summary>
    /// Show the Customer Management window as dialog
    /// </summary>
    public static bool? ShowCustomerDialog()
    {
        var window = new UtilityCustomerView();
        return window.ShowDialog();
    }
}