using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Base class for value converters providing safe defaults and helpers.
/// - Default ConvertBack returns DependencyProperty.UnsetValue.
/// - Helper to detect "Inverse" parameters.
/// </summary>
public abstract class BaseValueConverter : IValueConverter
{
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

    public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // By default, converters are one-way. Return UnsetValue to indicate not supported.
        return DependencyProperty.UnsetValue;
    }

    protected static bool IsInverse(object parameter)
        => parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true
           || parameter?.ToString() == "!";
}

/// <summary>
/// Base class for multi value converters with safe ConvertBack default.
/// </summary>
public abstract class BaseMultiValueConverter : IMultiValueConverter
{
    public abstract object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);

    public virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // Not supported by default
        return Array.Empty<object>();
    }
}
