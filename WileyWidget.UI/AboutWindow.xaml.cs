using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Enhanced modal dialog showing comprehensive application information, features, and technical details.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        // Prism's ViewModelLocator will auto-wire the AboutViewModel. When it's available, attach close action.
        DataContextChanged += (_, __) =>
        {
            if (DataContext is ViewModels.AboutViewModel vm)
            {
                vm.CloseAction = () => Close();
            }
        };
    }
}
