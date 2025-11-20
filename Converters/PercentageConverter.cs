using Microsoft.UI.Xaml.Data;
using System;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts a double to percentage string (e.g., 12.34%).
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                return $"{doubleValue:F2}%";
            }
            return "0.00%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
