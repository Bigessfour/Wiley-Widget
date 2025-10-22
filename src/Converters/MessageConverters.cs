#nullable enable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget;

internal static class ConverterUtilities
{
    private static readonly BrushConverter BrushConverter = new();
    private static readonly FontWeightConverter FontWeightConverter = new();

    public static Brush ParseBrush(string? token, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return fallback;
        }

        try
        {
            if (BrushConverter.ConvertFromString(token) is Brush brush)
            {
                if (brush is Freezable freezable && freezable.CanFreeze)
                {
                    freezable.Freeze();
                }

                return brush;
            }
        }
        catch
        {
            // Ignore parsing errors and fall back to default brush.
        }

        return fallback;
    }

    public static FontWeight ParseFontWeight(string? token, FontWeight fallback)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return fallback;
        }

        try
        {
            return (FontWeight)FontWeightConverter.ConvertFromString(token)!;
        }
        catch
        {
            return fallback;
        }
    }

    public static (string? TrueValue, string? FalseValue) SplitParameter(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return (null, null);
        }

        var parts = parameter.Split('|');
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], parts[1])
        };
    }
}

/// <summary>
/// Converter for user message background color
/// </summary>
public class UserMessageBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUser && isUser)
        {
            return new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Blue for user
        }
        return new SolidColorBrush(Color.FromRgb(224, 224, 224)); // Gray for AI
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for message alignment
/// </summary>
public class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isUser = value is bool user && user;

        if (parameter is string rawParameter)
        {
            var parameterToken = rawParameter.Trim().ToLowerInvariant();

            return parameterToken switch
            {
                "background" => isUser
                    ? ConverterUtilities.ParseBrush("#1976D2", Brushes.SteelBlue)
                    : ConverterUtilities.ParseBrush("#CFD8DC", Brushes.LightSlateGray),
                "avatar" => isUser ? "You" : "AI",
                _ => isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
        }

        return isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for message text color
/// </summary>
public class MessageForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (trueToken, falseToken) = ConverterUtilities.SplitParameter(parameter as string);
        var userBrush = ConverterUtilities.ParseBrush(trueToken, Brushes.White);
        var assistantBrush = ConverterUtilities.ParseBrush(falseToken, Brushes.Black);

        return value is bool isUser && isUser ? userBrush : assistantBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to visibility (inverse)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var comparisonParameter = (parameter as string)?.Trim();
        var visibility = EvaluateVisibility(value, comparisonParameter);
        return visibility ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static bool EvaluateVisibility(object? value, string? parameter)
    {
        bool baseResult = value switch
        {
            bool boolValue => boolValue,
            string stringValue => !string.IsNullOrWhiteSpace(stringValue),
            int intValue => intValue != 0,
            null => false,
            _ => true
        };

        if (string.IsNullOrWhiteSpace(parameter))
        {
            return baseResult;
        }

        if (parameter.Equals("invert", StringComparison.OrdinalIgnoreCase) || parameter == "!")
        {
            return !baseResult;
        }

        if (parameter.Equals("empty", StringComparison.OrdinalIgnoreCase) && value is string textValue)
        {
            return string.IsNullOrWhiteSpace(textValue);
        }

        if (parameter.Equals("notempty", StringComparison.OrdinalIgnoreCase) && value is string nonEmptyValue)
        {
            return !string.IsNullOrWhiteSpace(nonEmptyValue);
        }

        if (int.TryParse(parameter, out var target) && value is int count)
        {
            return count == target;
        }

        return baseResult;
    }
}

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
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for count to visibility
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count && parameter is string param)
        {
            int targetCount = int.Parse(param);
            return count == targetCount ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that inverts a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : true;
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
        return Binding.DoNothing;
    }
}
