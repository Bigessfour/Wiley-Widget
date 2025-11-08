#r "nuget: Prism.Core, 9.0.537"

// ViewModel Pattern Test for Wiley Widget
// Demonstrates Prism BindableBase usage

using System;
using System.ComponentModel;
using Prism.Mvvm;
using System.Collections.ObjectModel;

Console.WriteLine("=== Wiley Widget ViewModel Test ===\n");

// Define a ViewModel for Budget Entries
public class BudgetEntryViewModel : BindableBase
{
    private string _department;
    private decimal _budgetedAmount;
    private decimal _actualAmount;
    private string _category;

    public string Department
    {
        get => _department;
        set => SetProperty(ref _department, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public decimal BudgetedAmount
    {
        get => _budgetedAmount;
        set
        {
            SetProperty(ref _budgetedAmount, value);
            RaisePropertyChanged(nameof(Variance));
            RaisePropertyChanged(nameof(VariancePercentage));
            RaisePropertyChanged(nameof(Status));
        }
    }

    public decimal ActualAmount
    {
        get => _actualAmount;
        set
        {
            SetProperty(ref _actualAmount, value);
            RaisePropertyChanged(nameof(Variance));
            RaisePropertyChanged(nameof(VariancePercentage));
            RaisePropertyChanged(nameof(Status));
        }
    }

    // Computed properties
    public decimal Variance => ActualAmount - BudgetedAmount;

    public decimal VariancePercentage =>
        BudgetedAmount != 0 ? (Variance / BudgetedAmount) * 100 : 0;

    public string Status
    {
        get
        {
            if (Math.Abs(VariancePercentage) < 5) return "On Track";
            if (Variance < 0) return "Under Budget";
            return "Over Budget";
        }
    }
}

// Test the ViewModel
var vm = new BudgetEntryViewModel();
int propertyChangedCount = 0;

vm.PropertyChanged += (s, e) =>
{
    propertyChangedCount++;
    Console.WriteLine($"  ✓ PropertyChanged: {e.PropertyName}");
};

Console.WriteLine("Setting properties...");
vm.Department = "Police Department";
vm.Category = "Personnel";
vm.BudgetedAmount = 1_500_000m;
vm.ActualAmount = 1_425_000m;

Console.WriteLine($"\n📊 Budget Entry Details:");
Console.WriteLine($"  Department: {vm.Department}");
Console.WriteLine($"  Category: {vm.Category}");
Console.WriteLine($"  Budgeted: ${vm.BudgetedAmount:N2}");
Console.WriteLine($"  Actual: ${vm.ActualAmount:N2}");
Console.WriteLine($"  Variance: ${vm.Variance:N2}");
Console.WriteLine($"  Variance %: {vm.VariancePercentage:F2}%");
Console.WriteLine($"  Status: {vm.Status}");

Console.WriteLine($"\n✓ Total PropertyChanged events: {propertyChangedCount}");

// Verify correctness
var expectedVariance = -75_000m;
var expectedPercentage = -5.0m;

if (vm.Variance == expectedVariance &&
    Math.Abs(vm.VariancePercentage - expectedPercentage) < 0.01m)
{
    Console.WriteLine("\n✅ ViewModel test PASSED!");
    return "Success";
}
else
{
    Console.WriteLine("\n❌ ViewModel test FAILED!");
    return "Failed";
}
