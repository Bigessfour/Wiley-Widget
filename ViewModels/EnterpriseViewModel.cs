using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing municipal enterprises (Phase 1)
/// Provides data binding for enterprise CRUD operations and budget calculations
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
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(IEnterpriseRepository enterpriseRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Loads all enterprises from the database
    /// </summary>
    [RelayCommand]
    private async Task LoadEnterprisesAsync()
    {
        try
        {
            IsLoading = true;
            var enterprises = await _enterpriseRepository.GetAllAsync();

            Enterprises.Clear();
            foreach (var enterprise in enterprises)
            {
                Enterprises.Add(enterprise);
            }
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Console.WriteLine($"Error loading enterprises: {ex.Message}");
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
    private async Task AddEnterpriseAsync()
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
    private async Task SaveEnterpriseAsync()
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
    private async Task DeleteEnterpriseAsync()
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
        var totalCitizens = Enterprises.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue:F2}\n" +
               $"Total Expenses: ${totalExpenses:F2}\n" +
               $"Monthly Balance: ${totalBalance:F2}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
    }
}
