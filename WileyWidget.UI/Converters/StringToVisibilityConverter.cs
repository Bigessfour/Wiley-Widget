using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts string values to Visibility - shows element when string is not null/empty
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts string to Visibility
    /// </summary>
    /// <param name="value">String value</param>
    /// <param name="targetType">Target type (Visibility)</param>
    /// <param name="parameter">Optional parameter - if "Inverse", inverts the logic</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Visible if string is not null/empty (or empty if Inverse), Collapsed otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
        bool result = !string.IsNullOrEmpty(value as string);
        return isInverse ? (result ? Visibility.Collapsed : Visibility.Visible) : (result ? Visibility.Visible : Visibility.Collapsed);
    }

    /// <summary>
    /// Not implemented for one-way binding
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
