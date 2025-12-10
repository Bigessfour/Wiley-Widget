using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.Models;

/// <summary>
/// DTO for Budget Category display in the grid with full CRUD support.
/// Matches BudgetEntry entity but simplified for UI display.
/// </summary>
public partial class BudgetCategoryDto : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string accountNumber = string.Empty;

    [ObservableProperty]
    private decimal budgetedAmount;

    [ObservableProperty]
    private decimal actualAmount;

    [ObservableProperty]
    private decimal encumbranceAmount;

    [ObservableProperty]
    private int fiscalYear;

    [ObservableProperty]
    private string departmentName = string.Empty;

    [ObservableProperty]
    private string? fundName;

    // Computed properties
    public decimal Variance => BudgetedAmount - ActualAmount - EncumbranceAmount;

    public decimal PercentUsed
    {
        get
        {
            if (BudgetedAmount == 0) return 0;
            return ((ActualAmount + EncumbranceAmount) / BudgetedAmount);
        }
    }

    public string Status
    {
        get
        {
            var percentUsed = PercentUsed;
            if (percentUsed >= 1.0m) return "Over Budget";
            if (percentUsed >= 0.9m) return "Approaching Limit";
            if (percentUsed >= 0.75m) return "On Track";
            return "Under Budget";
        }
    }

    public string Trend
    {
        get
        {
            if (Variance >= 0) return "↗️"; // Up arrow - positive variance
            return "↘️"; // Down arrow - over budget
        }
    }

    // Notify computed properties when dependencies change
    partial void OnBudgetedAmountChanged(decimal value) => NotifyComputedProperties();
    partial void OnActualAmountChanged(decimal value) => NotifyComputedProperties();
    partial void OnEncumbranceAmountChanged(decimal value) => NotifyComputedProperties();

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(Variance));
        OnPropertyChanged(nameof(PercentUsed));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Trend));
    }
}
