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

namespace WileyWidget.Views.Main;

/// <summary>
/// Customer Management View - Provides full CRUD interface for utility customers
/// Prism auto-wires ViewModel via ViewModelLocator.AutoWireViewModel
/// </summary>
public partial class UtilityCustomerView : UserControl
{
    public UtilityCustomerView()
    {
        InitializeComponent();
    }
}
