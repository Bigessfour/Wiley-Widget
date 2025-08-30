using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Media;
using Serilog;

namespace WileyWidget.ViewModels;

/// <summary>
/// Enhanced view model for managing municipal enterprises with dashboard analytics
/// Provides comprehensive budget analysis, visual indicators, and actionable insights
/// </summary>
public partial class EnterpriseViewModel : ObservableObject
{
    private readonly IEnterpriseRepository _enterpriseRepository;

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
    /// Comprehensive budget metrics for dashboard display
    /// </summary>
    [ObservableProperty]
    private BudgetMetrics budgetMetrics = new();

    /// <summary>
    /// AI-powered budget insights and recommendations
    /// </summary>
    [ObservableProperty]
    private BudgetInsights budgetInsights = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(IEnterpriseRepository enterpriseRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        Log.Information("EnterpriseViewModel initialized with repository: {RepositoryType}", 
                       enterpriseRepository.GetType().Name);
    }

    /// <summary>
    /// Loads all enterprises from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadEnterprisesAsync()
    {
        try
        {
            IsLoading = true;
            Log.Information("Loading enterprises from database");

            var enterprises = await _enterpriseRepository.GetAllAsync();

            Enterprises.Clear();
            foreach (var enterprise in enterprises)
            {
                Enterprises.Add(enterprise);
                Log.Debug("Loaded enterprise: {Name} - Revenue: {Revenue}, Expenses: {Expenses}, Balance: {Balance}",
                         enterprise.Name, enterprise.MonthlyRevenue, enterprise.MonthlyExpenses, enterprise.MonthlyBalance);
            }

            // Calculate and update budget metrics
            CalculateBudgetMetrics();
            GenerateBudgetInsights();

            Log.Information("Successfully loaded {Count} enterprises", Enterprises.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load enterprises from database");
            // Continue with empty list rather than crashing
        }
        finally
        {
            IsLoading = false;
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
            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 0.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 0,
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);
            Enterprises.Add(addedEnterprise);
            SelectedEnterprise = addedEnterprise;
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Console.WriteLine($"Error adding enterprise: {ex.Message}");
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
            // MonthlyRevenue is now automatically calculated from CitizenCount * CurrentRate
            await _enterpriseRepository.UpdateAsync(SelectedEnterprise);
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Console.WriteLine($"Error saving enterprise: {ex.Message}");
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
            var success = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);
            if (success)
            {
                Enterprises.Remove(SelectedEnterprise);
                SelectedEnterprise = Enterprises.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Console.WriteLine($"Error deleting enterprise: {ex.Message}");
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
            BudgetMetrics = new BudgetMetrics();
            return;
        }

        var metrics = new BudgetMetrics
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
    private void GenerateBudgetInsights()
    {
        var insights = new BudgetInsights();
        insights.GenerateInsights(BudgetMetrics);
        BudgetInsights = insights;

        Log.Information("Budget insights generated with {RecommendationCount} recommendations",
                       insights.Recommendations.Count);
    }
}
