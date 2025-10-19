using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Enhanced modal dialog showing comprehensive application information, features, and technical details.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        ViewModels.AboutViewModel? viewModel = null;
        try
        {
            var containerProvider = App.GetContainerProvider();
            viewModel = containerProvider.Resolve<ViewModels.AboutViewModel>();
        }
        catch (InvalidOperationException)
        {
            viewModel = null;
        }

        // Get the ViewModel from DI container
        if (viewModel is ViewModels.AboutViewModel resolvedViewModel)
        {
            DataContext = resolvedViewModel;
            resolvedViewModel.CloseAction = () => Close();
        }
        else
        {
            // Fallback if DI fails - this should not happen in normal operation
            throw new InvalidOperationException("AboutViewModel could not be resolved from DI container");
        }
    }
}
