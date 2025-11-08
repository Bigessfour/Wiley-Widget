using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters
{
    /// <summary>
    /// Converts null/object values to Visibility - shows element when object is not null
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts object to Visibility
        /// </summary>
        /// <param name="value">Object value</param>
        /// <param name="targetType">Target type (Visibility)</param>
        /// <param name="parameter">Optional parameter - if "Inverse", inverts the logic</param>
        /// <param name="culture">Culture info</param>
        /// <returns>Visible if object is not null (or null if Inverse), Collapsed otherwise</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            bool result = value != null;
            return isInverse ? (result ? Visibility.Collapsed : Visibility.Visible) : (result ? Visibility.Visible : Visibility.Collapsed);
        }

        /// <summary>
        /// Not implemented for one-way binding
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
