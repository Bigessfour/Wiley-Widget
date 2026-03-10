using FluentAssertions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.LayerProof.Tests;

public sealed class OAuthContractTests
{
    [Fact]
    public void QuickBooksOAuthOptions_IsValid_WhenRequiredValuesArePresent()
    {
        var options = new QuickBooksOAuthOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost:5000/callback/",
        };

        options.IsValid.Should().BeTrue();
        options.Environment.Should().Be("sandbox");
    }

    [Fact]
    public void TokenResult_Success_PopulatesCompatibilityProperties()
    {
        var result = TokenResult.Success("access-token", "refresh-token", 1800);

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.ExpiresIn.Should().Be(1800);
    }

    [Fact]
    public void QuickBooksOAuthToken_IsExpiredOrSoonToExpire_RespectsBuffer()
    {
        var token = new QuickBooksOAuthToken
        {
            AccessToken = "access-token",
            ExpiresIn = 3600,
            RefreshTokenExpiresIn = 7200,
            IssuedAtUtc = DateTime.UtcNow.AddSeconds(-3500),
        };

        token.IsExpired.Should().BeFalse();
        token.IsExpiredOrSoonToExpire().Should().BeTrue();
        token.IsValid.Should().BeTrue();
    }
}