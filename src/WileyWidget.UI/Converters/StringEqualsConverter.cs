using System;
using System.Globalization;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts a string value to bool based on equality comparison
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    /// <summary>
    /// Converts string to bool if it equals the parameter
    /// </summary>
    /// <param name="value">String value to compare</param>
    /// <param name="targetType">Target type (bool)</param>
    /// <param name="parameter">String to compare against</param>
    /// <param name="culture">Culture info</param>
    /// <returns>True if strings match, false otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && parameter is string paramString)
        {
            return stringValue.Equals(paramString, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// ConvertBack is not supported for this converter as it cannot reliably convert back to the original string
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
