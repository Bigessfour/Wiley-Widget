using System.Windows;
using Prism.Dialogs;
using Prism.Mvvm;
using WileyWidget.Models;

namespace WileyWidget.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the Enterprise Dialog
/// </summary>
public partial class EnterpriseDialogViewModel : BindableBase, IDialogAware
{
    private Enterprise _enterprise = new();

    /// <summary>
    /// Gets or sets the enterprise being edited
    /// </summary>
    public Enterprise Enterprise
    {
        get => _enterprise;
        set
        {
            if (SetProperty(ref _enterprise, value))
            {
                OkCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(Name));
                RaisePropertyChanged(nameof(Description));
                RaisePropertyChanged(nameof(Type));
                RaisePropertyChanged(nameof(FiscalYearStart));
            }
        }
    }

    // Wrapper properties for dialog binding
    public string Name
    {
        get => Enterprise.Name;
        set
        {
            if (Enterprise.Name != value)
            {
                Enterprise.Name = value;
                RaisePropertyChanged();
                OkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Description
    {
        get => Enterprise.Description ?? string.Empty;
        set
        {
            if (Enterprise.Description != value)
            {
                Enterprise.Description = value;
                RaisePropertyChanged();
            }
        }
    }

    public string Type
    {
        get => Enterprise.Type ?? string.Empty;
        set
        {
            if (Enterprise.Type != value)
            {
                Enterprise.Type = value;
                RaisePropertyChanged();
                OkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime FiscalYearStart
    {
        get => _enterprise.CreatedDate;
        set
        {
            if (_enterprise.CreatedDate != value)
            {
                _enterprise.CreatedDate = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the OK command
    /// </summary>
    public Prism.Commands.DelegateCommand OkCommand { get; private set; }

    /// <summary>
    /// Gets the Cancel command
    /// </summary>
    public Prism.Commands.DelegateCommand CancelCommand { get; private set; }

    /// <summary>
    /// Callback to close the dialog
    /// </summary>
    public DialogCloseListener RequestClose { get; set; }

    /// <summary>
    /// Gets whether the dialog can be closed
    /// </summary>
    public bool CanCloseDialog() => true;

    /// <summary>
    /// Called when the dialog is opened
    /// </summary>
    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // Get the enterprise from dialog parameters if provided
        if (parameters.TryGetValue("Enterprise", out Enterprise enterprise))
        {
            Enterprise = enterprise;
        }
    }

    /// <summary>
    /// Called when the dialog is closed
    /// </summary>
    public void OnDialogClosed() { }

    /// <summary>
    /// Initializes a new instance of the EnterpriseDialogViewModel class
    /// </summary>
    public EnterpriseDialogViewModel()
    {
        OkCommand = new Prism.Commands.DelegateCommand(ExecuteOk, CanExecuteOk);
        CancelCommand = new Prism.Commands.DelegateCommand(ExecuteCancel);
    }

    private void ExecuteOk()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(Enterprise.Name))
        {
            MessageBox.Show("Enterprise name is required.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Enterprise.Type))
        {
            MessageBox.Show("Enterprise type is required.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Enterprise.CitizenCount <= 0)
        {
            MessageBox.Show("Citizen count must be greater than zero.", "Validation Error",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Close dialog with OK result
        var result = new DialogResult(ButtonResult.OK);
        result.Parameters.Add("Enterprise", Enterprise);
        RequestClose.Invoke(result);
    }

    private bool CanExecuteOk()
    {
        return !string.IsNullOrWhiteSpace(Enterprise.Name) &&
               !string.IsNullOrWhiteSpace(Enterprise.Type) &&
               Enterprise.CitizenCount > 0;
    }

    private void ExecuteCancel()
    {
        // Close dialog with Cancel result
        var result = new DialogResult(ButtonResult.Cancel);
        RequestClose.Invoke(result);
    }
}
