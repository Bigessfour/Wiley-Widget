using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converter for chat message background color based on author name
/// </summary>
public class AuthorBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string authorName)
        {
            // "You" = user message (right side, gray), others = AI message (left side, blue)
            return authorName == "You" ? "#F5F5F5" : "#E3F2FD";
        }
        return "#F5F5F5";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Converter for chat message alignment based on author name
/// </summary>
public class AuthorAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string authorName)
        {
            // "You" = user message (right side), others = AI message (left side)
            return authorName == "You" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
