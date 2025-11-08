using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts boolean values to Visibility - shows element when boolean is false (inverse of BooleanToVisibilityConverter)
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts boolean to Visibility (inverted logic)
    /// </summary>
    /// <param name="value">Boolean value</param>
    /// <param name="targetType">Target type (Visibility)</param>
    /// <param name="parameter">Not used</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Visible if false, Collapsed if true</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
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
