using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using WileyWidget.Services;
using WileyWidget.Services.Threading;

#nullable enable

namespace WileyWidget.ViewModels.Base;

/// <summary>
/// Base class for ViewModels that provides async functionality and property change notifications
/// </summary>
public abstract class AsyncViewModelBase : BindableBase, INotifyPropertyChanged
{
    /// <summary>
    /// Gets the logger instance
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the dispatcher helper
    /// </summary>
    protected IDispatcherHelper DispatcherHelper { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="dispatcherHelper">The dispatcher helper for UI thread operations</param>
    /// <param name="logger">The logger instance</param>
    protected AsyncViewModelBase(IDispatcherHelper dispatcherHelper, ILogger logger)
    {
        DispatcherHelper = dispatcherHelper ?? throw new ArgumentNullException(nameof(dispatcherHelper));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize standardized Load command
        LoadDataCommand = new DelegateCommand(async () =>
        {
            await ExecuteAsyncOperation(ct => OnLoadDataAsync(ct), "Loading data...");
        }, () => !IsBusy);
    }
    private bool _isBusy;
    private string? _busyMessage;

    /// <summary>
    /// Standardized load command for ViewModels to load or refresh data.
    /// </summary>
    public DelegateCommand LoadDataCommand { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the ViewModel is currently busy
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                RaisePropertyChanged();
                // Keep commands in sync with busy state
                LoadDataCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the busy message to display when the ViewModel is busy
    /// </summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        set
        {
            if (_busyMessage != value)
            {
                _busyMessage = value;
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// Executes an async operation while setting the busy state
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="busyMessage">Optional busy message to display</param>
    /// <returns>A task representing the async operation</returns>
    protected async Task ExecuteAsync(Func<Task> operation, string? busyMessage = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            IsBusy = true;
            BusyMessage = busyMessage;

            await operation();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Executes an async operation that returns a result while setting the busy state
    /// </summary>
    /// <typeparam name="T">The type of the result</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="busyMessage">Optional busy message to display</param>
    /// <returns>A task representing the async operation with result</returns>
    protected async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string? busyMessage = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            IsBusy = true;
            BusyMessage = busyMessage;

            return await operation();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Executes an async operation with cancellation support while setting the busy state
    /// </summary>
    /// <param name="operation">The async operation to execute with cancellation token</param>
    /// <param name="statusMessage">Optional status message to display</param>
    /// <returns>A task representing the async operation</returns>
    protected async Task ExecuteAsyncOperation(Func<CancellationToken, Task> operation, string? statusMessage = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            IsBusy = true;
            BusyMessage = statusMessage;

            using var cts = new CancellationTokenSource();
            await operation(cts.Token);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Hook for child ViewModels to implement their load logic in a unified way.
    /// Base implementation does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation</param>
    /// <returns>Task</returns>
    protected virtual Task OnLoadDataAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
