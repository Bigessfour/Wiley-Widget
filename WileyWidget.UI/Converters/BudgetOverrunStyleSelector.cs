using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WileyWidget.Models;

namespace WileyWidget.Converters;

/// <summary>
/// Style selector for conditional formatting of budget cells
/// </summary>
public class BudgetOverrunStyleSelector : StyleSelector
{
    /// <summary>
    /// Style for normal budget cells
    /// </summary>
    public Style NormalStyle { get; set; }

    /// <summary>
    /// Style for over-budget cells (red background)
    /// </summary>
    public Style OverBudgetStyle { get; set; }

    public override Style SelectStyle(object item, DependencyObject container)
    {
        if (item is Enterprise enterprise && container is DataGridCell cell)
        {
            // Check if this is a budget-related column
            if (cell.Column is DataGridTextColumn column &&
                (column.Header?.ToString()?.Contains("Budget", StringComparison.OrdinalIgnoreCase) == true ||
                 column.Header?.ToString()?.Contains("Expenses", StringComparison.OrdinalIgnoreCase) == true))
            {
                // Simple logic: if expenses > budget, mark as over budget
                // In a real app, this would be more sophisticated
                if (enterprise.MonthlyExpenses > enterprise.TotalBudget * 0.1M) // 10% of annual budget
                {
                    return OverBudgetStyle ?? CreateOverBudgetStyle();
                }
            }
        }

        return NormalStyle ?? CreateNormalStyle();
    }

    private Style CreateNormalStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        return style;
    }

    private Style CreateOverBudgetStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.LightCoral));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.DarkRed));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        return style;
    }
}
