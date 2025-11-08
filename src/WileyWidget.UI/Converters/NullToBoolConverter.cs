using System;
using System.Globalization;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts null values to boolean - returns true if value is not null
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    /// <summary>
    /// Converts value to boolean based on null check
    /// </summary>
    /// <param name="value">Value to check for null</param>
    /// <param name="targetType">Target type (bool)</param>
    /// <param name="parameter">Optional parameter - if "Inverse", inverts the logic</param>
    /// <param name="culture">Culture info</param>
    /// <returns>True if value is not null (or null if Inverse), false otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
        bool result = value != null;

        return isInverse ? !result : result;
    }

    /// <summary>
    /// ConvertBack is not supported for this converter as it cannot reliably convert back to the original object type
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
