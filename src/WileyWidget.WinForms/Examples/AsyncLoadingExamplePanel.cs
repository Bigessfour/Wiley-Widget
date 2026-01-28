#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.WinForms.Examples;

/// <summary>
/// Example panel demonstrating OnHandleCreatedAsync() for heavy async initialization without blocking the UI thread.
/// </summary>
/// <remarks>
/// This panel shows the recommended pattern for loading large datasets or making network calls after the panel is visible.
/// The ViewModel is resolved synchronously in OnHandleCreated, then heavy loading happens asynchronously in OnHandleCreatedAsync.
/// A progress indicator can be shown during the async load to provide user feedback.
/// </remarks>
public class AsyncLoadingExamplePanel : ScopedPanelBase<AsyncLoadingExampleViewModel>
{
    private Label? _loadingLabel;

    public AsyncLoadingExamplePanel(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedPanelBase<AsyncLoadingExampleViewModel>> logger)
        : base(scopeFactory, logger)
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _loadingLabel = new Label
        {
            Text = "Loading data...",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Visible = false
        };
        Controls.Add(_loadingLabel);
    }

    /// <summary>
    /// Override OnHandleCreatedAsync to perform heavy initialization without blocking the UI thread.
    /// This method is called asynchronously after the panel is displayed (via BeginInvoke).
    /// </summary>
    protected override async Task OnHandleCreatedAsync()
    {
        try
        {
            // Show loading indicator
            if (_loadingLabel != null)
            {
                InvokeOnUiThread(() => _loadingLabel.Visible = true);
            }

            // Simulate heavy data loading (e.g., database query, API call)
            await Task.Delay(2000); // Replace with actual async work

            if (ViewModel != null)
            {
                // Load data via ViewModel
                var ct = RegisterOperation();
                await LoadAsync(ct);
            }

            // Hide loading indicator
            if (_loadingLabel != null)
            {
                InvokeOnUiThread(() => _loadingLabel.Visible = false);
            }

            Logger.LogInformation("Async initialization completed for {PanelType}", GetType().Name);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Async initialization canceled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during async initialization for {PanelType}", GetType().Name);
        }
    }

    protected override void OnViewModelResolved(AsyncLoadingExampleViewModel viewModel)
    {
        base.OnViewModelResolved(viewModel);
        // Bind UI controls to ViewModel here (fast operations only)
        // Heavy operations should be deferred to OnHandleCreatedAsync
    }

    public override async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;

            // Simulate loading large dataset
            await Task.Delay(1000, ct);

            if (ViewModel != null)
            {
                // Load ViewModel data
                ViewModel.Items = new List<string> { "Item 1", "Item 2", "Item 3" };
            }

            SetHasUnsavedChanges(false);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// Example ViewModel for AsyncLoadingExamplePanel.
/// </summary>
public class AsyncLoadingExampleViewModel
{
    public string Title { get; set; } = "Async Loading Example";
    public List<string> Items { get; set; } = new();
    public bool IsLoading { get; set; }
}
