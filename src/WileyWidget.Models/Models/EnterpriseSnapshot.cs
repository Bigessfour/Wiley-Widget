namespace WileyWidget.Models;

public class EnterpriseSnapshot
{
    public string Name { get; set; } = string.Empty;        // "Water", "Sewer", etc.
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetPosition => Revenue - Expenses;
    public double BreakEvenRatio => Expenses > 0 ? (double)(Revenue / Expenses * 100) : 0;
    public bool IsSelfSustaining => NetPosition >= 0;
    public string CrossSubsidyNote { get; set; } = "Self-funded";
}
