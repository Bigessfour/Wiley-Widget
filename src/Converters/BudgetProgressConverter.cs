using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converter for scaling budget amounts to progress bar values (0-100)
/// </summary>
public class BudgetProgressConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal decimalAmount)
        {
            // Scale the budget amount to a 0-100 range for progress bar
            // Assuming max budget is around $100,000, scale accordingly
            const decimal maxBudget = 100000m;
            var scaledValue = (double)(decimalAmount / maxBudget) * 100;
            return Math.Min(Math.Max(scaledValue, 0.0), 100.0);
        }
        else if (value is double doubleAmount)
        {
            // Scale the budget amount to a 0-100 range for progress bar
            // Assuming max budget is around $100,000, scale accordingly
            const double maxBudget = 100000.0;
            var scaledValue = (doubleAmount / maxBudget) * 100;
            return Math.Min(Math.Max(scaledValue, 0.0), 100.0);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
