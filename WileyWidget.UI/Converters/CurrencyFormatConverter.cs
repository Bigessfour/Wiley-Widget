using System;
using System.Globalization;
using System.Windows.Data;

namespace WileyWidget;

/// <summary>
/// Converter for formatting currency values in charts
/// </summary>
public class CurrencyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            return decimalValue.ToString("C", CultureInfo.CurrentCulture);
        }
        else if (value is double doubleValue)
        {
            return doubleValue.ToString("C", CultureInfo.CurrentCulture);
        }
        else if (value is float floatValue)
        {
            return floatValue.ToString("C", CultureInfo.CurrentCulture);
        }
        else if (value is int intValue)
        {
            return intValue.ToString("C", CultureInfo.CurrentCulture);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
