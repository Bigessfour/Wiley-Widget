#nullable enable

using System.Windows.Input;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels.Base;

namespace WileyWidget.ViewModels.Main;

/// <summary>
/// ViewModel for displaying progress information
/// </summary>
public class ProgressViewModel : AsyncViewModelBase
{
    private string? _title;
    private string? _message;
    private int _progressPercentage;
    private bool _isIndeterminate;

    /// <summary>
    /// Gets or sets the progress title
    /// </summary>
    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Gets or sets the progress message
    /// </summary>
    public string? Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// Gets or sets the progress percentage (0-100)
    /// </summary>
    public int ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// Gets or sets a value indicating whether the progress is indeterminate
    /// </summary>
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetProperty(ref _isIndeterminate, value);
    }

    /// <summary>
    /// Gets the command to cancel the operation
    /// </summary>
    public Prism.Commands.DelegateCommand CancelCommand { get; }

    /// <summary>
    /// Gets or sets the action to cancel the operation
    /// </summary>
    public Action? CancelAction { get; set; }

    /// <summary>
    /// Initializes a new instance of the ProgressViewModel class
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations</param>
    /// <param name="logger">The logger instance</param>
    public ProgressViewModel(IDispatcherHelper dispatcherHelper, Microsoft.Extensions.Logging.ILogger<ProgressViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        CancelCommand = new Prism.Commands.DelegateCommand(Cancel, CanCancel);
        Title = "Operation in Progress";
        Message = "Please wait...";
        IsIndeterminate = true;
    }

    private bool CanCancel()
    {
        return !IsBusy;
    }

    private void Cancel()
    {
        CancelAction?.Invoke();
    }

    /// <summary>
    /// Updates the progress information
    /// </summary>
    /// <param name="message">The progress message</param>
    /// <param name="percentage">The progress percentage</param>
    public void UpdateProgress(string message, int percentage)
    {
        Message = message;
        ProgressPercentage = percentage;
        IsIndeterminate = false;
    }

    /// <summary>
    /// Sets the progress to indeterminate state
    /// </summary>
    /// <param name="message">The progress message</param>
    public void SetIndeterminate(string message)
    {
        Message = message;
        IsIndeterminate = true;
    }
}
