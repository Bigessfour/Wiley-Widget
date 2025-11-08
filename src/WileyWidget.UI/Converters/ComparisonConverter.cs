using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts a numeric value to comparison result (-1, 0, 1) based on comparison with parameter
/// </summary>
public class ComparisonConverter : IValueConverter
{
    /// <summary>
    /// Converts numeric value to comparison result
    /// </summary>
    /// <param name="value">Numeric value to compare</param>
    /// <param name="targetType">Target type (int)</param>
    /// <param name="parameter">Value to compare against</param>
    /// <param name="culture">Culture info</param>
    /// <returns>-1 if value < parameter, 0 if equal, 1 if value > parameter</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (double.TryParse(value?.ToString(), out double numValue) &&
            double.TryParse(parameter?.ToString(), out double paramValue))
        {
            if (numValue > paramValue)
            {
                return 1;
            }
            else if (numValue < paramValue)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// Not implemented
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
