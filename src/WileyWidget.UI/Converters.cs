using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Converters;

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

// BooleanToVisibilityConverter is implemented in src/Converters/BooleanToVisibilityConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

// ComparisonConverter is implemented in src/Converters/ComparisonConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

/// <summary>
/// Converts status messages to appropriate colors.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a status message string to a color based on its content.
    /// </summary>
    /// <param name="value">The status message string.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The appropriate color brush.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) || message.Contains("Failed", StringComparison.OrdinalIgnoreCase) || message.Contains("failed", StringComparison.OrdinalIgnoreCase))
                return Brushes.Red;
            if (message.Contains("Warning", StringComparison.OrdinalIgnoreCase) || message.Contains("warning", StringComparison.OrdinalIgnoreCase))
                return Brushes.Orange;
            if (message.Contains("Success", StringComparison.OrdinalIgnoreCase) || message.Contains("completed successfully", StringComparison.OrdinalIgnoreCase))
                return Brushes.Green;
        }

        return Brushes.Black; // Default color
    }

    /// <summary>
    /// ConvertBack is not supported for this converter.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>DependencyProperty.UnsetValue to indicate conversion back is not supported.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

// BalanceColorConverter is implemented in src/Converters/BalanceColorConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

/// <summary>
/// Converts enterprise status values to appropriate colors.
/// </summary>
public class StatusColorConverter : IValueConverter
{
    /// <summary>
    /// Converts an enterprise status to a color brush.
    /// </summary>
    /// <param name="value">The enterprise status value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter: "Light" for background colors.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Color brush based on status value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return new SolidColorBrush(Color.FromRgb(185, 200, 236)); // Neutral gray-blue

        var status = value.ToString();
        var param = parameter?.ToString() ?? string.Empty;

        // Light background colors for cells
        if (param == "Light")
        {
            switch (status)
            {
                case "Active":
                    return new SolidColorBrush(Color.FromArgb(26, 74, 222, 128)); // Light green
                case "Inactive":
                    return new SolidColorBrush(Color.FromArgb(26, 128, 128, 128)); // Light gray
                case "Suspended":
                    return new SolidColorBrush(Color.FromArgb(26, 255, 165, 0)); // Light orange
                default:
                    return new SolidColorBrush(Color.FromArgb(26, 59, 130, 246)); // Light blue
            }
        }

        // Standard foreground colors
        switch (status)
        {
            case "Active":
                return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
            case "Inactive":
                return new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray
            case "Suspended":
                return new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
            default:
                return new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue
        }
    }

    /// <summary>
    /// ConvertBack is not supported for this converter.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts a collection of MunicipalAccount objects to a unique list of Department objects.
/// </summary>
public class UniqueDepartmentsConverter : IValueConverter
{
    /// <summary>
    /// Converts a collection of MunicipalAccount to unique departments.
    /// </summary>
    /// <param name="value">The collection of MunicipalAccount objects.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>A collection of unique Department objects.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IEnumerable<WileyWidget.Models.MunicipalAccount> accounts)
        {
            var uniqueDepartments = accounts
                .Where(a => a.Department != null)
                .Select(a => a.Department)
                .Distinct()
                .OrderBy(d => d?.Name)
                .ToList();

            return uniqueDepartments;
        }

        return new List<WileyWidget.Models.Department>();
    }

    /// <summary>
    /// ConvertBack is not supported for this converter.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converts integer count to Visibility - shows element when count is zero
/// Used for empty state messages in data grids
/// </summary>
// ZeroToVisibleConverter is implemented in src/Converters/ZeroToVisibleConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

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
        if (value is SolidColorBrush brush)
        {
            // Check if the brush color matches the user message color (blue)
            var userColor = Color.FromRgb(25, 118, 210);
            return brush.Color.Equals(userColor);
        }
        return false;
    }
}

// BudgetProgressConverter is implemented in src/Converters/BudgetProgressConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

// StringToVisibilityConverter is implemented in src/Converters/StringToVisibilityConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

/// <summary>
/// Converts boolean values to Brush - returns success brush for true, error brush for false
/// </summary>
// BooleanToBrushConverter is implemented in src/Converters/BooleanToBrushConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

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
        var isUser = value is bool user && user;
        var (trueToken, falseToken) = ConverterUtilities.SplitParameter(parameter as string);
        var userBrush = ConverterUtilities.ParseBrush(trueToken, Brushes.White);
        var assistantBrush = ConverterUtilities.ParseBrush(falseToken, Brushes.Black);

        return isUser ? userBrush : assistantBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// GreaterThanConverter is implemented in src/Converters/GreaterThanConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

// NullToBoolConverter is implemented in src/Converters/NullToBoolConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

// InverseBooleanToVisibilityConverter is implemented in src/Converters/InverseBooleanToVisibilityConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.

// NullToVisibilityConverter is implemented in src/Converters/NullToVisibilityConverter.cs
// to avoid duplicate type definitions across projects. Keep single implementation there.
