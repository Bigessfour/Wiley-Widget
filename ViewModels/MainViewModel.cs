using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WileyWidget.ViewModels;

/// <summary>
/// Demonstration view model providing an in-memory list of widgets and a command to cycle selection.
/// Serves as a template for future data-bound collections / CRUD patterns.
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
    }

    [ObservableProperty]
    private bool quickBooksBusy;

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync()
    {
        if (_qb == null || QuickBooksBusy) return;
        try
        {
            QuickBooksBusy = true;
            var items = await _qb.GetCustomersAsync();
            QuickBooksCustomers.Clear();
            foreach (var c in items) QuickBooksCustomers.Add(c);
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksInvoicesAsync()
    {
        if (_qb == null || QuickBooksBusy) return;
        try
        {
            QuickBooksBusy = true;
            var items = await _qb.GetInvoicesAsync();
            QuickBooksInvoices.Clear();
            foreach (var i in items) QuickBooksInvoices.Add(i);
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }
}
