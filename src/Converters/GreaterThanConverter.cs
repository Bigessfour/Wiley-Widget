using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts a numeric value to Visibility based on greater than comparison
/// </summary>
public class GreaterThanConverter : IValueConverter
{
    /// <summary>
    /// Converts numeric value to Visibility if greater than parameter
    /// </summary>
    /// <param name="value">Numeric value to compare</param>
    /// <param name="targetType">Target type (Visibility)</param>
    /// <param name="parameter">Value to compare against</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Visible if value > parameter, Collapsed otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (double.TryParse(value?.ToString(), out double numValue) &&
            double.TryParse(parameter?.ToString(), out double paramValue))
        {
            return numValue > paramValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// Not implemented
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
