#nullable enable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Converters;

/// <summary>
/// Converter for profit/loss display
/// </summary>
public class ProfitLossTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? "Monthly Profit" : "Monthly Loss";
        }
        return "Monthly Position";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for profit/loss background color
/// </summary>
public class ProfitBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(232, 245, 232)) : new SolidColorBrush(Color.FromRgb(255, 243, 224));
        }
        return new SolidColorBrush(Color.FromRgb(245, 245, 245));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for profit/loss border color
/// </summary>
public class ProfitBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(56, 142, 60)) : new SolidColorBrush(Color.FromRgb(245, 124, 0));
        }
        return new SolidColorBrush(Color.FromRgb(189, 189, 189));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for profit/loss text color
/// </summary>
public class ProfitTextBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(56, 142, 60)) : new SolidColorBrush(Color.FromRgb(245, 124, 0));
        }
        return new SolidColorBrush(Color.FromRgb(33, 33, 33));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for boolean to background color
/// </summary>
public class BoolToBackgroundConverter : IValueConverter
{
    private static readonly Brush ErrorBrush;
    private static readonly Brush SuccessBrush;

    static BoolToBackgroundConverter()
    {
        ErrorBrush = new SolidColorBrush(Color.FromRgb(255, 235, 238));
        SuccessBrush = new SolidColorBrush(Color.FromRgb(232, 245, 232));

        ErrorBrush.Freeze();
        SuccessBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (trueToken, falseToken) = ConverterUtilities.SplitParameter(parameter as string);
        var trueBrush = ConverterUtilities.ParseBrush(trueToken, ErrorBrush);
        var falseBrush = ConverterUtilities.ParseBrush(falseToken, SuccessBrush);

        return value is bool condition && condition ? trueBrush : falseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

// Removed duplicate BoolToVisibilityConverter. Use WileyWidget.Converters.BooleanToVisibilityConverter instead.

/// <summary>
/// Converter for empty string to visibility
/// </summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for count to visibility
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            if (parameter is string param && int.TryParse(param, out var targetCount))
            {
                return count == targetCount ? Visibility.Visible : Visibility.Collapsed;
            }
            return DependencyProperty.UnsetValue; // invalid or missing parameter
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for boolean to foreground color
/// </summary>
public class BoolToForegroundConverter : IValueConverter
{
    private static readonly Brush ErrorBrush;
    private static readonly Brush SuccessBrush;

    static BoolToForegroundConverter()
    {
        ErrorBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47));
        SuccessBrush = new SolidColorBrush(Color.FromRgb(56, 142, 60));

        ErrorBrush.Freeze();
        SuccessBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (trueToken, falseToken) = ConverterUtilities.SplitParameter(parameter as string);
        var trueBrush = ConverterUtilities.ParseBrush(trueToken, ErrorBrush);
        var falseBrush = ConverterUtilities.ParseBrush(falseToken, SuccessBrush);

        return value is bool hasError && hasError ? trueBrush : falseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter that inverts a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter that maps boolean values to <see cref="FontWeight"/> instances.
/// </summary>
public class BooleanToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (trueToken, falseToken) = ConverterUtilities.SplitParameter(parameter as string);
        var trueWeight = ConverterUtilities.ParseFontWeight(trueToken, FontWeights.Bold);
        var falseWeight = ConverterUtilities.ParseFontWeight(falseToken, FontWeights.Normal);

        return value is bool flag && flag ? trueWeight : falseWeight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
