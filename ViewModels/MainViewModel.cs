using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using WileyWidget.Configuration;
using WileyWidget.Data;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using DotNetEnv;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration;

namespace WileyWidget.ViewModels;

    /// <summary>
    /// Enhanced main view model providing widgets, QuickBooks integration, and enterprise management
    /// Includes comprehensive logging for all operations and user interactions
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly QuickBooksService _qb;
        private readonly IConfiguration _config;
        private readonly GrokSupercomputer _grokSupercomputer;
        private readonly AppDbContext _dbContext;
        private readonly IEnterpriseRepository _enterpriseRepository;
        private readonly WpfMiddlewareService _middlewareService;
        private readonly EnterpriseViewModel _enterpriseViewModel;

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public MainViewModel(
            IConfiguration config,
            GrokSupercomputer grokSupercomputer = null,
            AppDbContext dbContext = null,
            IEnterpriseRepository enterpriseRepository = null,
            QuickBooksService quickBooksService = null,
            WpfMiddlewareService middlewareService = null)
        {
            Log.Information("MainViewModel initialization started with DI");

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _grokSupercomputer = grokSupercomputer;
            _dbContext = dbContext;
            _enterpriseRepository = enterpriseRepository;
            _qb = quickBooksService;
            _middlewareService = middlewareService ?? new WpfMiddlewareService();

            // Initialize enterprise management
            _enterpriseViewModel = new EnterpriseViewModel(_enterpriseRepository);

            // Initialize default widgets
            InitializeDefaultWidgets();

            Log.Information("MainViewModel initialized successfully with DI");
        }

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

        // Budget interactions properties
        public ObservableCollection<BudgetInteraction> BudgetInteractions { get; } = new();

        public Models.BudgetInsights BudgetInsights => _enterpriseViewModel?.BudgetInsights ?? new();

        /// <summary>Currently selected widget in the grid (null when none selected).</summary>
        [ObservableProperty]
        private Widget selectedWidget;

        /// <summary>Collection of widgets for the main view.</summary>
        public ObservableCollection<Widget> Widgets { get; } = new();

        /// <summary>Indicates if QuickBooks operations are in progress.</summary>
        [ObservableProperty]
        private bool quickBooksBusy;

        /// <summary>Collection of QuickBooks customers.</summary>
        public ObservableCollection<Customer> QuickBooksCustomers { get; } = new();

        /// <summary>Collection of QuickBooks invoices.</summary>
        public ObservableCollection<Invoice> QuickBooksInvoices { get; } = new();

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

    /// <summary>
    /// Legacy constructor for backward compatibility - creates dependencies manually
    /// </summary>
    [Obsolete("Use constructor with dependency injection parameters instead")]
    public MainViewModel() : this(
        ServiceLocator.GetService<IConfiguration>(),
        ServiceLocator.GetServiceOrDefault<GrokSupercomputer>(),
        ServiceLocator.GetServiceOrDefault<AppDbContext>(),
        ServiceLocator.GetServiceOrDefault<IEnterpriseRepository>(),
        ServiceLocator.GetServiceOrDefault<QuickBooksService>(),
        ServiceLocator.GetServiceOrDefault<WpfMiddlewareService>())
    {
        // If GrokSupercomputer wasn't provided by DI, create it manually with database service
        if (_grokSupercomputer == null && _config != null)
        {
            try
            {
                using var loggerFactory = new LoggerFactory();
                loggerFactory.AddSerilog(Log.Logger);
                var logger = loggerFactory.CreateLogger<GrokSupercomputer>();

                var contextFactory = new AppDbContextFactory();
                _dbContext = contextFactory.CreateDbContext(new string[0]);

                // Try to get the database service from DI
                var dbService = ServiceLocator.GetServiceOrDefault<GrokDatabaseService>();

                _grokSupercomputer = new GrokSupercomputer(_config, logger, _dbContext, dbService);
                Log.Information("GrokSupercomputer initialized successfully with database service");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize GrokSupercomputer - AI features will be disabled");
                _grokSupercomputer = null;
                _dbContext = null;
            }
        }
    }

    /// <summary>
    /// Initializes the default widgets for the application
    /// </summary>
    private void InitializeDefaultWidgets()
    {
        if (Widgets.Count == 0)
        {
            Widgets.Add(new Widget { Id = 1, Name = "Alpha", Category = "Test", Price = 10.00m });
            Widgets.Add(new Widget { Id = 2, Name = "Beta", Category = "Test", Price = 20.00m });
            Widgets.Add(new Widget { Id = 3, Name = "Gamma", Category = "Test", Price = 30.00m });
            
            SelectedWidget = Widgets[0];
            Log.Information("Initialized {WidgetCount} default widgets", Widgets.Count);
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadEnterprisesAsync()
    {
        await _middlewareService.ExecuteAsync("LoadEnterprises", async () =>
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
        });
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

    [RelayCommand]
    private async System.Threading.Tasks.Task RunGrokCrunchAsync()
    {
        if (_grokSupercomputer == null)
        {
            Log.Warning("Attempted to run Grok crunch but GrokSupercomputer is not available");
            return;
        }

        if (_enterpriseViewModel == null || _enterpriseViewModel.Enterprises.Count == 0)
        {
            Log.Warning("Attempted to run Grok crunch but no enterprises are available");
            return;
        }

        Log.Information("User initiated Grok AI crunch analysis for {Count} enterprises", 
                       _enterpriseViewModel.Enterprises.Count);

        try
        {
            // Define the algorithm for Grok to execute
            var algoDescription = @"
Calculate for each enterprise:
- Deficit = MonthlyExpenses - MonthlyRevenue
- If deficit > 0, SuggestedRateHike = (deficit / CitizenCount) * 1.1 (10% buffer)
- If deficit <= 0, SuggestedRateHike = 0
- Provide a witty suggestion for budget optimization

Output structured analysis with actionable insights.";

            // Run the AI-powered analysis
            var updatedEnterprises = await _grokSupercomputer.CrunchNumbersAsync(
                _enterpriseViewModel.Enterprises.ToList(), 
                algoDescription);

            // Update the enterprise collection (this will trigger UI refresh via ObservableCollection)
            _enterpriseViewModel.Enterprises.Clear();
            foreach (var enterprise in updatedEnterprises)
            {
                _enterpriseViewModel.Enterprises.Add(enterprise);
            }

            Log.Information("Grok crunch analysis completed successfully - {Count} enterprises updated", 
                           updatedEnterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Grok crunch analysis failed - falling back to local calculations");
            
            // Could implement local fallback here if desired
            // For now, just log the error and let user know
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CrunchWithGrokAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to crunch enterprises with Grok but enterprise management system is not available");
            return;
        }

        if (_grokSupercomputer == null)
        {
            Log.Warning("Attempted to crunch enterprises with Grok but GrokSupercomputer is not available");
            return;
        }

        Log.Information("User initiated Grok analysis for {Count} enterprises", _enterpriseViewModel.Enterprises.Count);

        try
        {
            // Get current enterprises list
            var enterprisesList = _enterpriseViewModel.Enterprises.ToList();

            // Call GrokSupercomputer to compute enterprises
            var analyzedEnterprises = await _grokSupercomputer.ComputeEnterprisesAsync(enterprisesList);

            // Update the Enterprises collection with analyzed data
            _enterpriseViewModel.Enterprises.Clear();
            foreach (var enterprise in analyzedEnterprises)
            {
                _enterpriseViewModel.Enterprises.Add(enterprise);
            }

            // Save to database
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);
            context.Enterprises.UpdateRange(analyzedEnterprises);
            await context.SaveChangesAsync();

            Log.Information("Grok analysis completed and saved to database for {Count} enterprises", analyzedEnterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to perform Grok analysis");
        }
    }

    [RelayCommand]
    private void TestConfigurationAsync()
    {
        try
        {
            var apiKey = _config["xAI:ApiKey"];
            var hasApiKey = !string.IsNullOrEmpty(apiKey) && !apiKey.Contains("your-xai-api-key-here");

            Log.Information("Configuration test - xAI API Key loaded: {HasKey}", hasApiKey);

            if (hasApiKey)
            {
                Log.Information("✅ xAI API key is properly configured and loaded from .env file");
            }
            else
            {
                Log.Warning("⚠️ xAI API key not found or still using placeholder. Please update .env file with your actual xAI API key");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to test configuration");
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadBudgetInteractionsAsync()
    {
        try
        {
            Log.Information("Loading budget interactions from database");

            // Get DbContext from the enterprise view model
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);

            var interactions = await context.BudgetInteractions
                .Include(bi => bi.PrimaryEnterprise)
                .Include(bi => bi.SecondaryEnterprise)
                .ToListAsync();

            BudgetInteractions.Clear();
            foreach (var interaction in interactions)
            {
                BudgetInteractions.Add(interaction);
                Log.Debug("Loaded budget interaction: {Type} - {Description} (${Amount})",
                         interaction.InteractionType, interaction.Description, interaction.MonthlyAmount);
            }

            Log.Information("Successfully loaded {Count} budget interactions", BudgetInteractions.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load budget interactions from database");
        }
    }

    [RelayCommand]
    private void RefreshDiagram()
    {
        Log.Information("User initiated diagram refresh");
        // The diagram will automatically refresh when BudgetInteractions collection changes
        // due to ObservableCollection notifications
        Log.Information("Diagram refresh completed - {Count} interactions available for visualization",
                       BudgetInteractions.Count);
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

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync()
    {
        if (_qb == null) return;
        try
        {
            QuickBooksBusy = true;
            var customers = await _qb.GetCustomersAsync();
            QuickBooksCustomers.Clear();
            foreach (var customer in customers)
            {
                QuickBooksCustomers.Add(customer);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load QuickBooks customers");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksInvoicesAsync()
    {
        if (_qb == null) return;
        try
        {
            QuickBooksBusy = true;
            var invoices = await _qb.GetInvoicesAsync();
            QuickBooksInvoices.Clear();
            foreach (var invoice in invoices)
            {
                QuickBooksInvoices.Add(invoice);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load QuickBooks invoices");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SyncEnterprisesToQbo()
    {
        if (_qb == null || _enterpriseViewModel == null) return;
        try
        {
            QuickBooksBusy = true;
            foreach (var enterprise in Enterprises)
            {
                await _qb.SyncEnterpriseToQboClassAsync(enterprise);
            }
            // Save changes to database
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync enterprises to QBO");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SyncBudgetInteractionsToQbo()
    {
        if (_qb == null) return;
        try
        {
            QuickBooksBusy = true;
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);
            var interactions = await context.BudgetInteractions.Include(bi => bi.PrimaryEnterprise).ToListAsync();
            foreach (var interaction in interactions)
            {
                var classId = interaction.PrimaryEnterprise?.QboClassId;
                if (!string.IsNullOrEmpty(classId))
                {
                    await _qb.SyncBudgetInteractionToQboAccountAsync(interaction, classId);
                }
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync budget interactions to QBO");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task AnalyzeBudgetWithGrokAsync()
    {
        if (_enterpriseViewModel == null)
        {
            Log.Warning("Attempted to analyze budget with Grok but enterprise management system is not available");
            return;
        }

        if (_grokSupercomputer == null)
        {
            Log.Warning("Attempted to analyze budget with Grok but GrokSupercomputer is not available");
            return;
        }

        Log.Information("User initiated comprehensive Grok budget analysis");

        try
        {
            var enterprisesList = _enterpriseViewModel.Enterprises.ToList();
            
            // Get advanced analytics from Grok
            var budgetMetrics = await _grokSupercomputer.ComputeBudgetAnalyticsAsync(enterprisesList);
            
            // Generate AI-powered insights
            var budgetInsights = await _grokSupercomputer.GenerateBudgetInsightsAsync(budgetMetrics, enterprisesList);
            
            // Update UI with results
            // Note: You'd need to bind these to UI properties
            
            Log.Information("Comprehensive Grok budget analysis completed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to perform comprehensive Grok budget analysis");
        }
    }

    [RelayCommand]
    private void ExportForCpa()
    {
        if (_enterpriseViewModel != null)
        {
            _enterpriseViewModel.ExportForCpa();
        }
    }

    /// <summary>
    /// Disposes of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed resources
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _grokSupercomputer?.Dispose();
            _dbContext?.Dispose();
            _enterpriseViewModel?.Dispose();
        }
    }

    /// <summary>
    /// Finalizer for MainViewModel
    /// </summary>
    ~MainViewModel()
    {
        Dispose(false);
    }
}
