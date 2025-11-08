using System.Globalization;
using System.Windows;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class StringToVisibilityConverterTests
{
    private readonly StringToVisibilityConverter _sut = new();

    [Theory]
    [InlineData("hello", Visibility.Visible)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void Convert_Should_Show_WhenNotNullOrEmpty(string? input, Visibility expected)
    {
        var result = _sut.Convert(input!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hello", Visibility.Collapsed)]
    [InlineData("", Visibility.Visible)]
    [InlineData(null, Visibility.Visible)]
    public void Convert_Should_Support_Inverse(string? input, Visibility expected)
    {
        var result = _sut.Convert(input!, typeof(Visibility), "Inverse", CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertBack_Should_ReturnUnsetValue()
    {
        var result = _sut.ConvertBack(Visibility.Visible, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be(DependencyProperty.UnsetValue);
    }
}
