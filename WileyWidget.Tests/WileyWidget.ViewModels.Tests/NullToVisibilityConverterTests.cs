using System.Windows;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter _sut = new();

    [Theory]
    [InlineData(null, Visibility.Collapsed)]
    public void Convert_Should_Collapse_When_Null(object? input, Visibility expected)
    {
        var result = _sut.Convert(input!, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void Convert_Should_Show_When_NotNull()
    {
        var result = _sut.Convert(new object(), typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_Should_Support_Inverse()
    {
        _sut.Convert(new object(), typeof(Visibility), "Inverse", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
        _sut.Convert(null!, typeof(Visibility), "Inverse", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);
    }
}
