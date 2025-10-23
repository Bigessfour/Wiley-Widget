using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Syncfusion.UI.Xaml.Charts;
using Syncfusion.SfSkinManager;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using Serilog;

namespace WileyWidget;

/// <summary>
/// High-impact analytics dashboard wiring Syncfusion visuals to Grok output.
/// </summary>
public partial class AnalyticsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsView"/> class.
    /// ViewModel is auto-wired by Prism ViewModelLocator.
    /// </summary>
    public AnalyticsView()
    {
        InitializeComponent();
    }
}
