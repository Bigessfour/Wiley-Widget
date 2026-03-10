using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// UI-facing allocation line for splitting a single budget entry across multiple funds.
/// Stored in-memory for now and projected by the BudgetPanel details child grid.
/// </summary>
public class BudgetAllocationLine
{
    public int? FundId { get; set; }

    public decimal AllocationPercentage { get; set; } = 1m;

    [NotMapped]
    public decimal ParentBudgetedAmount { get; set; }

    [NotMapped]
    public decimal ParentActualAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal AllocatedActual { get; set; }

    public string? Notes { get; set; }

    [NotMapped]
    public decimal AllocationVariance => AllocatedAmount - AllocatedActual;

    public void Recalculate(decimal parentBudgetedAmount, decimal parentActualAmount)
    {
        AllocationPercentage = Math.Clamp(AllocationPercentage, 0m, 1m);
        AllocatedAmount = Math.Round(parentBudgetedAmount * AllocationPercentage, 2);
        AllocatedActual = Math.Round(parentActualAmount * AllocationPercentage, 2);
    }
}
