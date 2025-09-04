using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Media;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.IO;
using WileyWidget.Services;
using System.Windows;
using Syncfusion.SfSkinManager;
using System.ComponentModel;
using System.Collections.Generic;

namespace WileyWidget.ViewModels;

/// <summary>
/// Enhanced view model for managing municipal enterprises with dashboard analytics
/// Provides comprehensive budget analysis, visual indicators, and actionable insights
/// </summary>
public partial class EnterpriseViewModel : ObservableObject, IDisposable, INotifyPropertyChanged
{
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly GrokSupercomputer grokSupercomputer;
    private readonly UI.Dialogs.IEnterpriseEditorDialog _editorDialog;
    private bool disposed;
    // Tracks last persisted names to allow reverting UI edits that violate uniqueness rules
    private readonly Dictionary<int, string> _originalNames = new();

    public EnterpriseViewModel(bool disposed)
    {
        this.disposed = disposed;
    }

    /// <summary>
    /// Collection of all enterprises for data binding
    /// </summary>
    public ObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Currently selected enterprise in the UI
    /// </summary>
    [ObservableProperty]
    private Enterprise selectedEnterprise;

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Progress indicator for long-running operations
    /// </summary>
    [ObservableProperty]
    private double loadingProgress;

    /// <summary>
    /// Loading message for user feedback
    /// </summary>
    [ObservableProperty]
    private string loadingMessage = "Loading...";

    /// <summary>
    /// Comprehensive budget metrics for dashboard display
    /// </summary>
    [ObservableProperty]
    private Models.BudgetMetrics budgetMetrics = new();

    /// <summary>
    /// AI-powered budget insights and recommendations
    /// </summary>
    [ObservableProperty]
    private Models.BudgetInsights budgetInsights = new();

    /// <summary>
    /// Current theme for Syncfusion controls
    /// </summary>
    [ObservableProperty]
    private string currentTheme = "FluentLight";

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(IEnterpriseRepository enterpriseRepository, UI.Dialogs.IEnterpriseEditorDialog editorDialog = null)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _editorDialog = editorDialog ?? new UI.Dialogs.NoOpEnterpriseEditorDialog();
        Log.Information("EnterpriseViewModel initialized with repository: {RepositoryType}",
                       enterpriseRepository.GetType().Name);

        // Initialize theme monitoring
        InitializeThemeMonitoring();

        // Try to initialize GrokSupercomputer if configuration is available
        try
        {
            // Build configuration for GrokSupercomputer
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<EnterpriseViewModel>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            grokSupercomputer = new GrokSupercomputer(configuration);
            Log.Information("GrokSupercomputer initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize GrokSupercomputer - AI features will be disabled");
            grokSupercomputer = null;
        }
    }

    /// <summary>
    /// Initialize theme monitoring for Syncfusion controls
    /// </summary>
    private void InitializeThemeMonitoring()
    {
        try
        {
            // Get current theme from SfSkinManager
            var currentThemeName = SfSkinManager.GetTheme(Application.Current.MainWindow);
            CurrentTheme = currentThemeName?.ToString() ?? "FluentLight";
            Log.Information("EnterpriseViewModel initialized with theme: {Theme}", CurrentTheme);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize theme monitoring - using default theme");
            CurrentTheme = "FluentLight";
        }
    }

    /// <summary>
    /// Command to handle SfDataGrid SelectionChanged event
    /// </summary>
    [RelayCommand]
    public void HandleSelectionChanged()
    {
        if (SelectedEnterprise != null)
        {
            Log.Information("Enterprise selection changed to: {Name} (ID: {Id})",
                           SelectedEnterprise.Name, SelectedEnterprise.Id);
            OnPropertyChanged(nameof(SelectedEnterprise));
        }
    }

    /// <summary>
    /// Command to handle SfDataGrid RowDoubleTapped event
    /// </summary>
    [RelayCommand]
    public void HandleRowDoubleTapped()
    {
        if (SelectedEnterprise != null)
        {
            Log.Information("Enterprise double-clicked: {Name} - launching edit workflow placeholder",
                           SelectedEnterprise.Name);
            // Placeholder: In future, inject IEnterpriseEditorDialog to open modal editor
            // For now we log intent only to avoid blocking UI with unimplemented dialog.
        }
    }

    /// <summary>
    /// Loads all enterprises from the database with progress tracking
    /// </summary>
    [RelayCommand]
    public async Task LoadEnterprisesAsync()
    {
        try
        {
            IsLoading = true;
            LoadingProgress = 0;
            LoadingMessage = "Initializing enterprise loading...";
            Log.Information("Loading enterprises from database");

            LoadingProgress = 25;
            LoadingMessage = "Connecting to database...";

            var enterprises = await _enterpriseRepository.GetAllAsync();

            LoadingProgress = 50;
            LoadingMessage = "Processing enterprise data...";

            Enterprises.Clear();
            foreach (var enterprise in enterprises)
            {
                Enterprises.Add(enterprise);
                // Capture baseline persisted name
                if (!_originalNames.ContainsKey(enterprise.Id))
                    _originalNames[enterprise.Id] = enterprise.Name;
                Log.Debug("Loaded enterprise: {Name} - Revenue: {Revenue}, Expenses: {Expenses}, Balance: {Balance}",
                         enterprise.Name, enterprise.MonthlyRevenue, enterprise.MonthlyExpenses, enterprise.MonthlyBalance);
            }

            LoadingProgress = 75;
            LoadingMessage = "Calculating budget metrics...";

            // Calculate and update budget metrics
            CalculateBudgetMetrics();

            LoadingProgress = 90;
            LoadingMessage = "Generating AI insights...";

            await GenerateBudgetInsightsAsync();

            LoadingProgress = 100;
            LoadingMessage = "Loading complete";

            Log.Information("Successfully loaded {Count} enterprises", Enterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load enterprises from database");
            LoadingMessage = "Error loading enterprises";
            // Continue with empty list rather than crashing
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = 0;
        }
    }

    /// <summary>
    /// Saves all enterprises to the database
    /// </summary>
    private async Task SaveEnterprisesAsync()
    {
        try
        {
            foreach (var enterprise in Enterprises)
            {
                await _enterpriseRepository.UpdateAsync(enterprise);
            }
            Log.Information("Successfully saved {Count} enterprises after Grok analysis", Enterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save enterprises after Grok analysis");
        }
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    [RelayCommand]
    public async Task AddEnterpriseAsync()
    {
        try
        {
            var baseEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 15.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 0,
                Notes = "New enterprise - update details"
            };

            // Allow editor dialog to modify initial values
            var edited = _editorDialog.Show(baseEnterprise) ?? baseEnterprise;

            if (string.IsNullOrWhiteSpace(edited.Name))
            {
                Log.Warning("Aborting add enterprise - name required");
                return;
            }

            // Uniqueness check
            var exists = await _enterpriseRepository.ExistsByNameAsync(edited.Name);
            if (exists)
            {
                Log.Warning("Cannot add enterprise - name already exists: {Name}", edited.Name);
                return;
            }

            var addedEnterprise = await _enterpriseRepository.AddAsync(edited);
            Enterprises.Add(addedEnterprise);
            SelectedEnterprise = addedEnterprise;
            _originalNames[addedEnterprise.Id] = addedEnterprise.Name;
            Log.Information("Enterprise added: {Name} (ID: {Id})", addedEnterprise.Name, addedEnterprise.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add new enterprise");
            // Non-fatal: leave UI state unchanged
        }
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>
    [RelayCommand]
    public async Task SaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(SelectedEnterprise.Name))
            {
                Log.Warning("Cannot save enterprise with empty name (ID: {Id})", SelectedEnterprise.Id);
                return;
            }

            // Preserve original name so we can revert on conflict (object instance already mutated in UI)
            _originalNames.TryGetValue(SelectedEnterprise.Id, out var originalName);

            var nameConflict = await _enterpriseRepository.ExistsByNameAsync(SelectedEnterprise.Name, SelectedEnterprise.Id);
            if (nameConflict)
            {
                Log.Warning("Cannot save enterprise - name conflict: {Name}. Reverting to previous persisted name: {Original}", SelectedEnterprise.Name, originalName);
                if (!string.IsNullOrWhiteSpace(originalName))
                {
                    SelectedEnterprise.Name = originalName; // revert mutation so repository state reflects expected uniqueness
                }
                return;
            }

            await _enterpriseRepository.UpdateAsync(SelectedEnterprise);
            // Update persisted name snapshot after successful save
            _originalNames[SelectedEnterprise.Id] = SelectedEnterprise.Name;
            Log.Information("Enterprise saved: {Name} (ID: {Id})", SelectedEnterprise.Name, SelectedEnterprise.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save enterprise {Id}", SelectedEnterprise?.Id);
        }
    }

    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>
    [RelayCommand]
    public async Task DeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        try
        {
            var targetId = SelectedEnterprise.Id;
            var success = await _enterpriseRepository.DeleteAsync(targetId);
            if (success)
            {
                var removedName = SelectedEnterprise.Name;
                Enterprises.Remove(SelectedEnterprise);
                SelectedEnterprise = Enterprises.FirstOrDefault();
                Log.Information("Enterprise deleted: {Name} (ID: {Id})", removedName, targetId);
            }
            else
            {
                Log.Warning("Delete operation returned false for enterprise {Id}", targetId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete enterprise {Id}", SelectedEnterprise?.Id);
        }
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    public string GetBudgetSummary()
    {
        if (!Enterprises.Any())
            return "No enterprises loaded";

        var totalRevenue = Enterprises.Sum(e => e.MonthlyRevenue);
        var totalExpenses = Enterprises.Sum(e => e.MonthlyExpenses);
        var totalBalance = totalRevenue - totalExpenses;
        var totalDeficit = totalExpenses - totalRevenue;
        var totalCitizens = Enterprises.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue:F2}\n" +
               $"Total Expenses: ${totalExpenses:F2}\n" +
               $"Monthly Balance: ${totalBalance:F2}\n" +
               $"Monthly Deficit: ${totalDeficit:F2}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
    }

    /// <summary>
    /// Calculates comprehensive budget metrics for dashboard display
    /// </summary>
    private void CalculateBudgetMetrics()
    {
        if (!Enterprises.Any())
        {
            BudgetMetrics = new Models.BudgetMetrics();
            return;
        }

        var metrics = new Models.BudgetMetrics
        {
            TotalRevenue = Enterprises.Sum(e => e.MonthlyRevenue),
            TotalExpenses = Enterprises.Sum(e => e.MonthlyExpenses),
            TotalCitizens = Enterprises.Sum(e => e.CitizenCount)
        };

        BudgetMetrics = metrics;
        Log.Information("Budget metrics calculated - Revenue: {Revenue}, Expenses: {Expenses}, Balance: {Balance}",
                       metrics.TotalRevenue, metrics.TotalExpenses, metrics.MonthlyBalance);
    }

    /// <summary>
    /// Generates AI-powered budget insights and recommendations
    /// </summary>
    private async Task GenerateBudgetInsightsAsync()
    {
        var insights = new Models.BudgetInsights();
        insights.GenerateInsights(BudgetMetrics);
        BudgetInsights = insights;

        // Call Grok for advanced analysis
        try
        {
            var enterprisesList = Enterprises.ToList();
            var analyzedEnterprises = await grokSupercomputer.ComputeEnterprisesAsync(enterprisesList);

            // Update the collection with analyzed data
            for (int i = 0; i < Enterprises.Count; i++)
            {
                Enterprises[i] = analyzedEnterprises[i];
            }

            // Save the updated enterprises to the database
            await SaveEnterprisesAsync();

            Log.Information("Grok analysis completed for {EnterpriseCount} enterprises", Enterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to perform Grok analysis");
            // Continue without Grok insights
        }

        Log.Information("Budget insights generated with {RecommendationCount} recommendations",
                       insights.Recommendations.Count);
    }

    /// <summary>
    /// Exports budget insights for CPA review
    /// </summary>
    public void ExportForCpa()
    {
        try
        {
            // Show message box with export information
            var message = $"Budget Insights Export for CPA Review:\n\n" +
                         $"Main Insight: {BudgetInsights.MainInsight}\n\n" +
                         $"Recommendations:\n{string.Join("\n", BudgetInsights.Recommendations)}\n\n" +
                         $"Disclaimer: {BudgetInsights.Disclaimer}\n\n" +
                         $"This data has been logged for audit purposes.";

            // Since this is WPF, use MessageBox
            MessageBox.Show(message, "CPA Export", MessageBoxButton.OK, MessageBoxImage.Information);

            Log.Information("Budget insights exported for CPA review");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export for CPA review");
        }
    }

    /// <summary>
    /// Disposes the ViewModel and its resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Safely dispose GrokSupercomputer with null check
                try
                {
                    grokSupercomputer?.Dispose();
                    Log.Debug("GrokSupercomputer disposed successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing GrokSupercomputer");
                }
            }
            disposed = true;
        }
    }
}
