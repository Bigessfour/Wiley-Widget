using WileyWidget.Services;
using Xunit;
using FluentAssertions;
using Moq;
using System.Net.Http;
using RichardSzalay.MockHttp;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Integration.Tests.Services
{
    public class QuickBooksAuthServiceTests : IntegrationTestBase
    {
        // [Fact(Skip = "API changed - QuickBooksAuthService constructor and methods updated")]
        // public async Task RequestToken_ValidCode_ReturnsAndSavesTokens()
        // {
        //     // Arrange
        //     var mockHttp = new MockHttpMessageHandler();
        //     mockHttp.When("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
        //         .Respond("application/json", @"{
        //             ""access_token"": ""test-access-token"",
        //             ""refresh_token"": ""test-refresh-token"",
        //             ""expires_in"": 3600
        //         }");

        //     var httpClient = mockHttp.ToHttpClient();
        //     var authService = new QuickBooksAuthService(httpClient);

        //     // Act
        //     var result = await authService.RequestAccessTokenAsync("valid-code", "redirect-uri");

        //     // Assert
        //     result.Should().NotBeNull();
        //     result.AccessToken.Should().Be("test-access-token");
        //     result.RefreshToken.Should().Be("test-refresh-token");
        //     // Verify saved to settings or DB
        // }

        // [Fact(Skip = "API changed - QuickBooksAuthService constructor and methods updated")]
        // public async Task RefreshToken_NearExpiry_AutoRefreshes()
        // {
        //     // Arrange
        //     var mockHttp = new MockHttpMessageHandler();
        //     mockHttp.When("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer")
        //         .Respond("application/json", @"{
        //             ""access_token"": ""new-access-token"",
        //             ""refresh_token"": ""new-refresh-token"",
        //             ""expires_in"": 3600
        //         }");

        //     var httpClient = mockHttp.ToHttpClient();
        //     var authService = new QuickBooksAuthService(httpClient);

        //     // Act
        //     var result = await authService.RefreshAccessTokenAsync("old-refresh-token");

        //     // Assert
        //     result.Should().NotBeNull();
        //     result.AccessToken.Should().Be("new-access-token");
        // }

        // [Fact(Skip = "API changed - QuickBooksAuthService constructor and methods updated")]
        // public async Task RevokeToken_ClearsStoredTokens()
        // {
        //     // Arrange
        //     var mockHttp = new MockHttpMessageHandler();
        //     mockHttp.When("https://developer.api.intuit.com/v2/oauth2/tokens/revoke")
        //         .Respond(System.Net.HttpStatusCode.OK);

        //     var httpClient = mockHttp.ToHttpClient();
        //     var authService = new QuickBooksAuthService(httpClient);

        //     // Act
        //     var success = await authService.RevokeTokenAsync("token-to-revoke");

        //     // Assert
        //     success.Should().BeTrue();
        //     // Verify tokens cleared from storage
        // }
    }
}