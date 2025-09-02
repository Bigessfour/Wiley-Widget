using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Converters;

/// <summary>
/// Converts balance values to appropriate colors for visual indicators
/// Positive = Green, Negative = Red, Zero = Orange
/// </summary>
public class BalanceToColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a decimal balance to a color brush
    /// </summary>
    /// <param name="value">The balance value (decimal)</param>
    /// <param name="targetType">Target type (not used)</param>
    /// <param name="parameter">Parameter (not used)</param>
    /// <param name="culture">Culture (not used)</param>
    /// <returns>Green for positive, Red for negative, Orange for zero</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal balance)
        {
            if (balance > 0)
                return Brushes.Green;
            else if (balance < 0)
                return Brushes.Red;
            else
                return Brushes.Orange;
        }
        return Brushes.Black;
    }

    /// <summary>
    /// ConvertBack is not implemented for this one-way converter
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts budget status strings to appropriate colors
/// Surplus = Green, Deficit = Red, Break-even = Orange
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a status string to a color brush
    /// </summary>
    /// <param name="value">The status string</param>
    /// <param name="targetType">Target type (not used)</param>
    /// <param name="parameter">Parameter (not used)</param>
    /// <param name="culture">Culture (not used)</param>
    /// <returns>Green for surplus, Red for deficit, Orange for break-even</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower(CultureInfo.InvariantCulture) switch
            {
                "surplus" => Brushes.Green,
                "deficit" => Brushes.Red,
                "break-even" => Brushes.Orange,
                _ => Brushes.Black
            };
        }
        return Brushes.Black;
    }

    /// <summary>
    /// ConvertBack is not implemented for this one-way converter
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
