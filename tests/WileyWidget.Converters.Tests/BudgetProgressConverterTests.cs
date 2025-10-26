using System.Globalization;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class BudgetProgressConverterTests
{
    private readonly BudgetProgressConverter _sut = new();

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(50000d, 50d)]
    [InlineData(100000d, 100d)]
    [InlineData(150000d, 100d)] // clamped
    [InlineData(-500d, 0d)] // clamped
    public void Convert_Should_Scale_And_Clamp_Double(double input, double expected)
    {
        var result = (double)_sut.Convert(input, typeof(double), null!, CultureInfo.InvariantCulture);
        result.Should().BeApproximately(expected, 0.0001);
    }

    public static TheoryData<decimal, double> DecimalCases => new()
    {
        { 0m, 0d },
        { 50000m, 50d },
        { 100000m, 100d },
        { 150000m, 100d }, // clamped
        { -500m, 0d } // clamped
    };

    [Theory]
    [MemberData(nameof(DecimalCases))]
    public void Convert_Should_Scale_And_Clamp_Decimal(decimal input, double expected)
    {
        var result = (double)_sut.Convert(input, typeof(double), null!, CultureInfo.InvariantCulture);
        result.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Convert_Should_ReturnZero_OnInvalidType()
    {
        var result = (double)_sut.Convert("not-number", typeof(double), null!, CultureInfo.InvariantCulture);
        result.Should().Be(0d);
    }
}
