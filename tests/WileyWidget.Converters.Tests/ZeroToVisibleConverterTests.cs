using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class ZeroToVisibleConverterTests
{
    private readonly IValueConverter _sut = new ZeroToVisibleConverter();

    [Theory]
    [InlineData(0, Visibility.Visible)]
    [InlineData(1, Visibility.Collapsed)]
    [InlineData(10, Visibility.Collapsed)]
    public void Convert_Should_Show_WhenZero(int input, Visibility expected)
    {
        var result = _sut.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_Should_Fallback_ToCollapsed_OnInvalidType()
    {
        var result = _sut.Convert("not-int", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void ConvertBack_Should_ReturnUnsetValue()
    {
        var result = _sut.ConvertBack(Visibility.Visible, typeof(int), null!, CultureInfo.InvariantCulture);
        result.Should().Be(DependencyProperty.UnsetValue);
    }
}
