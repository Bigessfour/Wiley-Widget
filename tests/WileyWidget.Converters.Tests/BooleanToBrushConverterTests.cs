using System.Windows.Media;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class BooleanToBrushConverterTests
{
    private readonly BooleanToBrushConverter _sut = new();

    [Fact]
    public void Convert_Should_Return_Success_For_True()
    {
        var result = (Brush)_sut.Convert(true, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        // Default fallback is Brushes.Green when no application resources
        result.Should().Be(Brushes.Green);
    }

    [Fact]
    public void Convert_Should_Return_Error_For_False()
    {
        var result = (Brush)_sut.Convert(false, typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        // Default fallback is Brushes.Red when no application resources
        result.Should().Be(Brushes.Red);
    }

    [Fact]
    public void Convert_Should_Return_Gray_For_Invalid()
    {
        var result = (Brush)_sut.Convert("n/a", typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }
}
