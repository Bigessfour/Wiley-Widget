using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.Models;

/// <summary>
/// Model representing a department's rate analysis with current vs suggested charges.
/// Supports reactive UI updates via INotifyPropertyChanged.
/// </summary>
public partial class DepartmentRateModel : ObservableObject
{
    /// <summary>
    /// Department name: "Water", "Sewer", "Trash", or "Apartments"
    /// </summary>
    [ObservableProperty]
    private string department = string.Empty;

    /// <summary>
    /// Monthly expenses for this department (from QuickBooks)
    /// </summary>
    [ObservableProperty]
    private decimal monthlyExpenses;

    /// <summary>
    /// Current monthly charge per customer/unit
    /// </summary>
    [ObservableProperty]
    private decimal currentCharge;

    /// <summary>
    /// Suggested monthly charge based on expenses + AI adjustment factor
    /// </summary>
    [ObservableProperty]
    private decimal suggestedCharge;

    /// <summary>
    /// Monthly gain/loss: (CurrentCharge - MonthlyExpenses)
    /// Positive = profitable, negative = losing money
    /// </summary>
    [ObservableProperty]
    private decimal monthlyGainLoss;

    /// <summary>
    /// Position status text: "Losing Money", "Breaking Even", "Profitable"
    /// </summary>
    [ObservableProperty]
    private string positionStatus = "Unknown";

    /// <summary>
    /// Position color for UI binding: "Red", "Orange", "Green"
    /// </summary>
    [ObservableProperty]
    private string positionColor = "Gray";

    /// <summary>
    /// AI-recommended adjustment factor (e.g., 1.1 for 10% profit margin)
    /// Default: 1.0 (break-even)
    /// </summary>
    [ObservableProperty]
    private decimal aiAdjustmentFactor = 1.0m;

    /// <summary>
    /// Number of customers/units for this department (for total calculations)
    /// </summary>
    [ObservableProperty]
    private int customerCount;

    /// <summary>
    /// State-wide average rate for this department type
    /// </summary>
    [ObservableProperty]
    private decimal stateAverage;

    /// <summary>
    /// Variance from state average (%)
    /// </summary>
    public decimal VarianceFromState => StateAverage > 0
        ? ((CurrentCharge - StateAverage) / StateAverage * 100)
        : 0;

    /// <summary>
    /// Total monthly impact across all customers
    /// </summary>
    public decimal TotalMonthlyImpact => MonthlyGainLoss * CustomerCount;

    /// <summary>
    /// Updates the suggested charge based on expenses and AI adjustment factor
    /// </summary>
    public void UpdateSuggested(decimal aiFactor = 1.0m)
    {
        AiAdjustmentFactor = aiFactor;
        CalculateDerived();
    }

    /// <summary>
    /// Recalculates all derived properties when expenses or current charge changes
    /// </summary>
    partial void OnMonthlyExpensesChanged(decimal value) => CalculateDerived();
    partial void OnCurrentChargeChanged(decimal value) => CalculateDerived();
    partial void OnAiAdjustmentFactorChanged(decimal value) => CalculateDerived();
    partial void OnCustomerCountChanged(int value) => OnPropertyChanged(nameof(TotalMonthlyImpact));
    partial void OnStateAverageChanged(decimal value) => OnPropertyChanged(nameof(VarianceFromState));

    private void CalculateDerived()
    {
        SuggestedCharge = MonthlyExpenses * AiAdjustmentFactor;
        MonthlyGainLoss = CurrentCharge - MonthlyExpenses;

        // Determine position status
        const decimal breakEvenThreshold = 0.01m;
        if (MonthlyGainLoss < -breakEvenThreshold)
        {
            PositionStatus = "Losing Money";
            PositionColor = "Red";
        }
        else if (Math.Abs(MonthlyGainLoss) <= breakEvenThreshold)
        {
            PositionStatus = "Breaking Even";
            PositionColor = "Orange";
        }
        else
        {
            PositionStatus = "Profitable";
            PositionColor = "Green";
        }

        OnPropertyChanged(nameof(VarianceFromState));
        OnPropertyChanged(nameof(TotalMonthlyImpact));
    }
}
