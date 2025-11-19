using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts a hex color string (e.g., "#28A745") to a SolidColorBrush.
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string colorString && !string.IsNullOrEmpty(colorString))
            {
                // Parse hex color (#RRGGBB)
                if (colorString.StartsWith("#", StringComparison.Ordinal) && colorString.Length == 7)
                {
                    try
                    {
                        byte r = System.Convert.ToByte(colorString.Substring(1, 2), 16);
                        byte g = System.Convert.ToByte(colorString.Substring(3, 2), 16);
                        byte b = System.Convert.ToByte(colorString.Substring(5, 2), 16);
                        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
                    }
                    catch (Exception)
                    {
                        // Fall through to default gray
                    }
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
