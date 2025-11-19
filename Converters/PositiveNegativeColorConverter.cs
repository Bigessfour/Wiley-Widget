using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts a decimal amount to a color brush based on positive/negative value.
    /// Positive = Green, Negative = Red.
    /// </summary>
    public class PositiveNegativeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal amount)
            {
                return amount >= 0 
                    ? new SolidColorBrush(Color.FromArgb(255, 40, 167, 69))  // Green #28A745
                    : new SolidColorBrush(Color.FromArgb(255, 220, 53, 69)); // Red #DC3545
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
