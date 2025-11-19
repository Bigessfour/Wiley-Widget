using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts a number to a color brush (positive = green, negative = red, zero = gray).
    /// </summary>
    public class NumberToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal decimalValue)
            {
                if (decimalValue > 0)
                    return new SolidColorBrush(Color.FromArgb(255, 40, 167, 69)); // Green
                else if (decimalValue < 0)
                    return new SolidColorBrush(Color.FromArgb(255, 220, 53, 69)); // Red
                else
                    return new SolidColorBrush(Colors.Gray);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
