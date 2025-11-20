using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts a decimal to currency string (e.g., $1,234.56).
    /// </summary>
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString("C2", CultureInfo.CurrentCulture);
            }
            return "$0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
