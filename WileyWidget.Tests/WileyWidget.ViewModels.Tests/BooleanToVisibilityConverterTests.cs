using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class BooleanToVisibilityConverterTests
{
    private readonly BooleanToVisibilityConverter _sut = new();

    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void Convert_Should_MapBoolToVisibility(bool input, Visibility expected)
    {
        var result = _sut.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Inverse", Visibility.Collapsed)]
    [InlineData(false, "Inverse", Visibility.Visible)]
    [InlineData(true, "!", Visibility.Collapsed)]
    [InlineData(false, "!", Visibility.Visible)]
    public void Convert_Should_Support_Inverse_Parameter(bool input, object parameter, Visibility expected)
    {
        var result = _sut.Convert(input, typeof(Visibility), parameter, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Visibility.Visible, true)]
    [InlineData(Visibility.Collapsed, false)]
    [InlineData(Visibility.Hidden, false)]
    public void ConvertBack_Should_MapVisibilityToBool(Visibility input, bool expected)
    {
        var result = _sut.ConvertBack(input, typeof(bool), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Visibility.Visible, "Inverse", false)]
    [InlineData(Visibility.Collapsed, "Inverse", true)]
    [InlineData(Visibility.Visible, "!", false)]
    [InlineData(Visibility.Collapsed, "!", true)]
    public void ConvertBack_Should_Support_Inverse_Parameter(Visibility input, object parameter, bool expected)
    {
        var result = _sut.ConvertBack(input, typeof(bool), parameter, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_Should_Fallback_ToCollapsed_OnInvalidType()
    {
        var result = _sut.Convert("not-bool", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void ConvertBack_Should_ReturnUnsetValue_OnInvalidType()
    {
        var result = _sut.ConvertBack("not-visibility", typeof(bool), null!, CultureInfo.InvariantCulture);
        result.Should().Be(DependencyProperty.UnsetValue);
    }
}
