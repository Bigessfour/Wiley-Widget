using Microsoft.UI.Xaml.Data;
using System;

namespace WileyWidget.WinUI.Converters
{
    /// <summary>
    /// Converter that formats values using standard .NET format strings.
    /// This replaces the StringFormat functionality from WPF which is not available in WinUI 3.
    /// </summary>
    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string format && value != null)
            {
                try
                {
                    // Handle the format string which may include the {} escape sequence
                    var formatString = format.StartsWith("{}") ? format.Substring(2) : format;
                    return string.Format(formatString, value);
                }
                catch
                {
                    return value?.ToString() ?? string.Empty;
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
