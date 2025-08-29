using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using WileyWidget.Data;
using Serilog;

namespace WileyWidget.ViewModels;

    /// <summary>
    /// Enhanced main view model providing widgets, QuickBooks integration, and enterprise management
    /// Includes comprehensive logging for all operations and user interactions
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly QuickBooksService _qb; // null until user config provided

        public ObservableCollection<Widget> Widgets { get; } = new()
        {
            new Widget { Id = 1, Name = "Alpha", Category = "Core", Price = 19.99M },
            new Widget { Id = 2, Name = "Beta", Category = "Core", Price = 24.50M },
            new Widget { Id = 3, Name = "Gamma", Category = "Extended", Price = 42.00M }
        };

        public ObservableCollection<Customer> QuickBooksCustomers { get; } = new();
        public ObservableCollection<Invoice> QuickBooksInvoices { get; } = new();

        // Enterprise management properties
        private readonly EnterpriseViewModel _enterpriseViewModel;

        public ObservableCollection<Enterprise> Enterprises => _enterpriseViewModel?.Enterprises ?? new();
        public Enterprise SelectedEnterprise
        {
            get => _enterpriseViewModel?.SelectedEnterprise;
            set
            {
                if (_enterpriseViewModel != null)
                    _enterpriseViewModel.SelectedEnterprise = value;
            }
        }

        /// <summary>Currently selected widget in the grid (null when none selected).</summary>
        [ObservableProperty]
        private Widget selectedWidget;

    [RelayCommand]
    /// <summary>
    /// Cycles to the next widget (wrap-around). If none selected, selects the first. Safe for empty list.
    /// Logs all selection changes for user behavior analysis.
    /// </summary>
    private void SelectNext()
    {
        if (Widgets.Count == 0)
        {
            Log.Warning("Attempted to select next widget but widget list is empty");
            return;
        }

        if (SelectedWidget == null)
        {
            SelectedWidget = Widgets[0];
            Log.Information("Selected first widget: {WidgetName} (ID: {WidgetId})", 
                           SelectedWidget.Name, SelectedWidget.Id);
            return;
        }

        var previousIndex = Widgets.IndexOf(SelectedWidget);
        var idx = (previousIndex + 1) % Widgets.Count;
        SelectedWidget = Widgets[idx];

        Log.Information("Widget selection changed from {PreviousWidget} to {CurrentWidget}",
                       Widgets[previousIndex].Name, SelectedWidget.Name);
    }

    [RelayCommand]
    /// <summary>
    /// Adds a sample widget with incremental Id for quick UI testing (non-persistent demo data).
    /// Logs widget creation for audit trail and usage analytics.
    /// </summary>
    private void AddWidget()
    {
        var nextId = Widgets.Count == 0 ? 1 : Widgets[^1].Id + 1;
        var category = nextId % 2 == 0 ? "Core" : "Extended";
        var price = 10M + nextId * 1.5M;

        var w = new Widget
        {
            Id = nextId,
            Name = $"Widget {nextId}",
            Category = category,
            Price = price
        };

        Widgets.Add(w);
        SelectedWidget = w;

        Log.Information("New widget added - ID: {WidgetId}, Name: {WidgetName}, Category: {Category}, Price: {Price:C}",
                       w.Id, w.Name, w.Category, w.Price);
        Log.Debug("Widget collection now contains {WidgetCount} items", Widgets.Count);
    }

    public MainViewModel()
    {
        Log.Information("MainViewModel initialization started");

        // Load QuickBooks client id/secret from environment (user sets manually). Redirect port chosen arbitrarily (must match Intuit app settings).
        var cid = System.Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
        var csec = System.Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
        var redirect = System.Environment.GetEnvironmentVariable("QBO_REDIRECT_URI");

        if (string.IsNullOrWhiteSpace(redirect))
            redirect = "http://localhost:8080/callback/"; // default; MUST exactly match developer portal entry

        if (!redirect.EndsWith('/')) redirect += "/"; // HttpListener prefix requires trailing slash

        // Only initialize service if client id present.
        if (!string.IsNullOrWhiteSpace(cid))
        {
            _qb = new QuickBooksService(SettingsService.Instance);
            Log.Information("QuickBooks service initialized successfully");
        }
        else
        {
            Log.Warning("QuickBooks client ID not found in environment variables - QuickBooks features will be disabled");
        }

        // Initialize enterprise management
        try
        {
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);
            var enterpriseRepository = new EnterpriseRepository(context);
            _enterpriseViewModel = new EnterpriseViewModel(enterpriseRepository);
            Log.Information("Enterprise management system initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize enterprise management system - enterprise features will be disabled");
            _enterpriseViewModel = null;
        }

        Log.Information("MainViewModel initialization completed - Widgets: {WidgetCount}, Enterprises: {EnterpriseAvailable}",
                       Widgets.Count, _enterpriseViewModel != null ? "Available" : "Unavailable");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadEnterprisesAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to load enterprises but enterprise management system is not available");
            return;
        }

        Log.Information("User initiated enterprise loading");
        await _enterpriseViewModel.LoadEnterprisesAsync();

        var enterpriseCount = _enterpriseViewModel.Enterprises.Count;
        Log.Information("Enterprise loading completed - {Count} enterprises loaded", enterpriseCount);

        if (enterpriseCount > 0)
        {
            var totalRevenue = _enterpriseViewModel.Enterprises.Sum(e => e.MonthlyRevenue);
            var totalExpenses = _enterpriseViewModel.Enterprises.Sum(e => e.MonthlyExpenses);
            var totalBalance = totalRevenue - totalExpenses;

            Log.Information("Enterprise summary - Revenue: {Revenue:C}, Expenses: {Expenses:C}, Balance: {Balance:C}",
                           totalRevenue, totalExpenses, totalBalance);
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task AddEnterpriseAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to add enterprise but enterprise management system is not available");
            return;
        }

        Log.Information("User initiated enterprise addition");
        await _enterpriseViewModel.AddEnterpriseAsync();
        Log.Information("Enterprise addition process completed");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveEnterpriseAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to save enterprise but enterprise management system is not available");
            return;
        }

        var selectedEnterprise = _enterpriseViewModel.SelectedEnterprise;
        if (selectedEnterprise != null)
        {
            Log.Information("User initiated enterprise save - Enterprise: {Name} (ID: {Id})", 
                           selectedEnterprise.Name, selectedEnterprise.Id);
        }
        else
        {
            Log.Warning("User attempted to save enterprise but no enterprise is selected");
        }

        await _enterpriseViewModel.SaveEnterpriseAsync();
        Log.Information("Enterprise save process completed");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DeleteEnterpriseAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to delete enterprise but enterprise management system is not available");
            return;
        }

        var selectedEnterprise = _enterpriseViewModel.SelectedEnterprise;
        if (selectedEnterprise != null)
        {
            Log.Warning("User initiated enterprise deletion - Enterprise: {Name} (ID: {Id}) - THIS ACTION CANNOT BE UNDONE",
                       selectedEnterprise.Name, selectedEnterprise.Id);
        }
        else
        {
            Log.Warning("User attempted to delete enterprise but no enterprise is selected");
        }

        await _enterpriseViewModel.DeleteEnterpriseAsync();
        Log.Information("Enterprise deletion process completed");
    }

    /// <summary>
    /// Gets the budget summary from enterprise data
    /// Logs when budget summary is accessed for performance monitoring
    /// </summary>
    public string BudgetSummary
    {
        get
        {
            var summary = _enterpriseViewModel?.GetBudgetSummary() ?? "Enterprise data not available";
            Log.Debug("Budget summary accessed - Content length: {Length} characters", summary.Length);
            return summary;
        }
    }
}
