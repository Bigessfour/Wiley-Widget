using FluentAssertions;
using WileyWidget.Abstractions;

namespace WileyWidget.LayerProof.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GenericFailure_WithException_AppendsExceptionMessage()
    {
        var result = Result<string>.Failure("Token refresh failed", new InvalidOperationException("upstream timeout"));

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.ErrorMessage.Should().Be("Token refresh failed: upstream timeout");
    }
}