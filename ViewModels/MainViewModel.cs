using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using WileyWidget.Data;

namespace WileyWidget.ViewModels;

/// <summary>
/// Enhanced main view model providing widgets, QuickBooks integration, and enterprise management
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
    /// </summary>
    private void SelectNext()
    {
        if (Widgets.Count == 0)
            return;
        if (SelectedWidget == null)
        {
            SelectedWidget = Widgets[0];
            return;
        }
        var idx = Widgets.IndexOf(SelectedWidget);
        idx = (idx + 1) % Widgets.Count;
        SelectedWidget = Widgets[idx];
    }

    [RelayCommand]
    /// <summary>
    /// Adds a sample widget with incremental Id for quick UI testing (non-persistent demo data).
    /// </summary>
    private void AddWidget()
    {
        var nextId = Widgets.Count == 0 ? 1 : Widgets[^1].Id + 1;
        var w = new Widget
        {
            Id = nextId,
            Name = $"Widget {nextId}",
            Category = nextId % 2 == 0 ? "Core" : "Extended",
            Price = 10M + nextId * 1.5M
        };
        Widgets.Add(w);
        SelectedWidget = w;
    }

    public MainViewModel()
    {
        // Load QuickBooks client id/secret from environment (user sets manually). Redirect port chosen arbitrarily (must match Intuit app settings).
        var cid = System.Environment.GetEnvironmentVariable("QBO_CLIENT_ID");
        var csec = System.Environment.GetEnvironmentVariable("QBO_CLIENT_SECRET");
        var redirect = System.Environment.GetEnvironmentVariable("QBO_REDIRECT_URI");
        if (string.IsNullOrWhiteSpace(redirect))
            redirect = "http://localhost:8080/callback/"; // default; MUST exactly match developer portal entry
        if (!redirect.EndsWith('/')) redirect += "/"; // HttpListener prefix requires trailing slash
        // Only initialize service if client id present.
        if (!string.IsNullOrWhiteSpace(cid))
            _qb = new QuickBooksService(SettingsService.Instance);

        // Initialize enterprise management
        try
        {
            var contextFactory = new AppDbContextFactory();
            using var context = contextFactory.CreateDbContext(new string[0]);
            var enterpriseRepository = new EnterpriseRepository(context);
            _enterpriseViewModel = new EnterpriseViewModel(enterpriseRepository);
        }
        catch (Exception ex)
        {
            // Log error but don't fail - enterprise features will be disabled
            Console.WriteLine($"Failed to initialize enterprise management: {ex.Message}");
            _enterpriseViewModel = null;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadEnterprisesAsync()
    {
        if (_enterpriseViewModel != null)
            await _enterpriseViewModel.LoadEnterprisesAsync();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task AddEnterpriseAsync()
    {
        if (_enterpriseViewModel != null)
            await _enterpriseViewModel.AddEnterpriseAsync();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveEnterpriseAsync()
    {
        if (_enterpriseViewModel != null)
            await _enterpriseViewModel.SaveEnterpriseAsync();
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DeleteEnterpriseAsync()
    {
        if (_enterpriseViewModel != null)
            await _enterpriseViewModel.DeleteEnterpriseAsync();
    }

    /// <summary>
    /// Gets the budget summary from enterprise data
    /// </summary>
    public string BudgetSummary => _enterpriseViewModel?.GetBudgetSummary() ?? "Enterprise data not available";
}
