using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts integer count to Visibility - shows element when count is zero
/// Used for empty state messages in data grids
/// </summary>
public class ZeroToVisibleConverter : IValueConverter
{
    /// <summary>
    /// Converts integer count to Visibility (explicit interface implementation to avoid duplicate-member collisions)
    /// </summary>
    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// Not implemented for one-way binding (explicit interface implementation)
    /// </summary>
    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
