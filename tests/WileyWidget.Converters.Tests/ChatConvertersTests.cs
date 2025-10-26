using System.Windows;
using FluentAssertions;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Converters.Tests;

public class ChatConvertersTests
{
    [Theory]
    [InlineData("You", "#F5F5F5")]
    [InlineData("AI", "#E3F2FD")]
    [InlineData("Other", "#E3F2FD")]
    public void AuthorBackgroundConverter_Should_Map(string author, string expected)
    {
        var sut = new AuthorBackgroundConverter();
        var result = (string)sut.Convert(author, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("You", HorizontalAlignment.Right)]
    [InlineData("AI", HorizontalAlignment.Left)]
    [InlineData("Other", HorizontalAlignment.Left)]
    public void AuthorAlignmentConverter_Should_Map(string author, HorizontalAlignment expected)
    {
        var sut = new AuthorAlignmentConverter();
        var result = (HorizontalAlignment)sut.Convert(author, typeof(HorizontalAlignment), null!, System.Globalization.CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }
}
