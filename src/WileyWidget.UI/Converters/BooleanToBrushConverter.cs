using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Converters;

/// <summary>
/// Converts boolean values to Brush - returns success brush for true, error brush for false
/// </summary>
public class BooleanToBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts boolean to Brush
    /// </summary>
    /// <param name="value">Boolean value</param>
    /// <param name="targetType">Target type (Brush)</param>
    /// <param name="parameter">Optional parameter</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Success brush if true, error brush if false</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue)
            {
                // Use theme-aware brushes instead of hardcoded colors
                var successBrush = Application.Current?.TryFindResource("SuccessForegroundBrush") as Brush ?? Brushes.Green;
                var errorBrush = Application.Current?.TryFindResource("ErrorForegroundBrush") as Brush ?? Brushes.Red;

                return boolValue ? successBrush : errorBrush;
            }

            return Application.Current?.TryFindResource("DisabledForegroundBrush") as Brush ?? Brushes.Gray; // Default for non-boolean values
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    /// <summary>
    /// Not implemented - one way converter
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
