using System.Windows.Media;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class BalanceColorConverterTests
{
    private readonly BalanceColorConverter _sut = new();

    [Fact]
    public void Convert_Should_Return_Green_For_Positive()
    {
        var result = _sut.Convert(10m, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Green);
    }

    [Fact]
    public void Convert_Should_Return_Red_For_Negative()
    {
        var result = _sut.Convert(-1.0, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Red);
    }

    [Fact]
    public void Convert_Should_Return_Gray_For_Zero()
    {
        var result = _sut.Convert(0, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }

    [Fact]
    public void Convert_Should_Return_Gray_For_Invalid()
    {
        var result = _sut.Convert("n/a", typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }
}
