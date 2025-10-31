using System.Windows;
using System.Windows.Controls;
using WileyWidget.ViewModels;
using WileyWidget.ViewModels.Main;

namespace WileyWidget.Views.Main;

/// <summary>
/// Progress window for displaying operation progress
/// </summary>
public partial class ProgressView : Window
{
    private ProgressViewModel? _viewModel;

    public ProgressView()
    {
        InitializeComponent();

        // Get the ViewModel
        _viewModel = DataContext as ProgressViewModel;
        if (_viewModel != null)
        {
            // Set up cancel action to close the window
            _viewModel.CancelAction = () => Dispatcher.Invoke(Close);
        }

        // Handle window closing
        Closing += ProgressView_Closing;
    }

    private void ProgressView_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Clean up the ViewModel
        if (_viewModel != null)
        {
            _viewModel.CancelAction = null;
        }
    }

    /// <summary>
    /// Shows the progress window as a dialog
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <returns>Dialog result</returns>
    public bool? ShowProgressDialog(Window owner)
    {
        Owner = owner;
        return ShowDialog();
    }
}
