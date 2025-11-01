using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Prism.Ioc;
//using Syncfusion.Windows.Reports.Viewer;
using WileyWidget.ViewModels;

namespace WileyWidget.Views.Main;

/// <summary>
/// Interactive report surface powered by Syncfusion controls.
/// </summary>
public partial class ReportsView : UserControl
{
    /// <summary>
    /// Parameterless constructor remains for XAML designer compatibility and for
    /// any code paths that instantiate the view without DI. It attempts to use
    /// the application container as a fallback but does not throw if unavailable.
    /// </summary>
    public ReportsView()
    {
        InitializeComponent();
    }

    // Event handlers and private methods removed to enforce MVVM. Logic moved to ViewModel.
}
