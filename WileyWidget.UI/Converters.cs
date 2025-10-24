using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Views;

/// <summary>
/// Converts boolean values to Visibility.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean to Visibility.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The visibility value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    /// <summary>
    /// Converts back from Visibility to boolean.
    /// </summary>
    /// <param name="value">The visibility value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The boolean value.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts values for comparison operations.
/// </summary>
public class ComparisonConverter : IValueConverter
{
    /// <summary>
    /// Converts a value by comparing it to a parameter.
    /// Returns 1 if value > parameter, -1 if value < parameter, 0 if equal.
    /// </summary>
    /// <param name="value">The value to compare.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The comparison parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>1 for greater than, -1 for less than, 0 for equal.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return 0;

        // Try to parse the parameter
        if (!decimal.TryParse(parameter.ToString(), out var compareValue))
            return 0;

        // Convert value to decimal for comparison
        decimal actualValue = 0;
        if (value is decimal dec)
            actualValue = dec;
        else if (decimal.TryParse(value.ToString(), out var parsed))
            actualValue = parsed;

        return actualValue.CompareTo(compareValue);
    }

    /// <summary>
    /// Not implemented - this converter is intended for one-way conversion only.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The original value unchanged.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This converter is designed for one-way conversion (comparison results)
        // ConvertBack doesn't make logical sense for comparison operations
        return value;
    }
}

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
            if (message.Contains("Error") || message.Contains("Failed") || message.Contains("failed"))
                return Brushes.Red;
            if (message.Contains("Warning") || message.Contains("warning"))
                return Brushes.Orange;
            if (message.Contains("Success") || message.Contains("completed successfully"))
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

/// <summary>
/// Converts balance values to appropriate colors based on positive/negative values.
/// </summary>
public class BalanceColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a numeric balance to a color brush.
    /// </summary>
    /// <param name="value">The balance value (decimal or double).</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">Optional parameter: "Light" for background, "Negative" for negative check.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Color brush based on balance value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return new SolidColorBrush(Color.FromRgb(185, 200, 236)); // Neutral gray-blue

        decimal balance = 0;
        if (value is decimal dec)
            balance = dec;
        else if (value is double dbl)
            balance = (decimal)dbl;
        else if (value is int intVal)
            balance = intVal;
        else if (decimal.TryParse(value.ToString(), out var parsed))
            balance = parsed;

        var param = parameter?.ToString() ?? string.Empty;

        // Handle special parameters
        if (param == "PositiveVisibility")
            return balance > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (param == "NegativeVisibility")
            return balance < 0 ? Visibility.Visible : Visibility.Collapsed;

        if (param == "Negative")
            return balance < 0 ? "Negative" : "Positive";

        // Light background colors for cells
        if (param == "Light")
        {
            if (balance > 0)
                return new SolidColorBrush(Color.FromArgb(26, 74, 222, 128)); // Light green
            if (balance < 0)
                return new SolidColorBrush(Color.FromArgb(26, 248, 113, 113)); // Light red
            return new SolidColorBrush(Color.FromArgb(26, 59, 130, 246)); // Light blue
        }

        // Standard foreground colors
        if (balance > 0)
            return new SolidColorBrush(Color.FromRgb(74, 222, 128)); // Green
        if (balance < 0)
            return new SolidColorBrush(Color.FromRgb(248, 113, 113)); // Red

        return new SolidColorBrush(Color.FromRgb(185, 200, 236)); // Neutral gray-blue
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
