using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

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
        if (value is bool boolValue)
        {
            // Use theme-aware brushes instead of hardcoded colors
            var successBrush = (Brush)Application.Current.FindResource("SuccessForegroundBrush");
            var errorBrush = (Brush)Application.Current.FindResource("ErrorForegroundBrush");
            
            return boolValue ? successBrush : errorBrush;
        }

        return (Brush)Application.Current.FindResource("DisabledForegroundBrush"); // Default for non-boolean values
    }

    /// <summary>
    /// Not implemented - one way converter
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}