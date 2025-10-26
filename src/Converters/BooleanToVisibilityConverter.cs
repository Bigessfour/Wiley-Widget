using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts boolean values to Visibility - shows element when boolean is true
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts boolean to Visibility
    /// </summary>
    /// <param name="value">Boolean value</param>
    /// <param name="targetType">Target type (Visibility)</param>
    /// <param name="parameter">Optional parameter - if "Inverse", inverts the logic</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Visible if true (or false if Inverse), Collapsed otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true
                             || parameter?.ToString() == "!";
            bool result = isInverse ? !boolValue : boolValue;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// Converts Visibility back to boolean
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true
                             || parameter?.ToString() == "!";
            bool result = visibility == Visibility.Visible;
            return isInverse ? !result : result;
        }

        // Unsupported input -> signal binding engine to ignore
        return DependencyProperty.UnsetValue;
    }
}
