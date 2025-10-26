using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Converters;

/// <summary>
/// Converter for displaying balance amounts with appropriate colors:
/// - Positive balances: Green
/// - Negative balances: Red
/// - Zero balances: Gray
/// </summary>
public class BalanceColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            if (decimalValue > 0)
                return Brushes.Green;
            else if (decimalValue < 0)
                return Brushes.Red;
            else
                return Brushes.Gray;
        }
        else if (value is double doubleValue)
        {
            if (doubleValue > 0)
                return Brushes.Green;
            else if (doubleValue < 0)
                return Brushes.Red;
            else
                return Brushes.Gray;
        }
        else if (value is int intValue)
        {
            if (intValue > 0)
                return Brushes.Green;
            else if (intValue < 0)
                return Brushes.Red;
            else
                return Brushes.Gray;
        }

        return Brushes.Gray; // Default for unknown types
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
