using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace WileyWidget.Webhooks.Tests;

public sealed class WebhooksEndpointsTests
{
    [Fact]
    public async Task Health_ReturnsOkStatusPayload()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task LandingPage_ReturnsHtml()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Webhooks_SandboxWithoutVerifier_AcceptsUnsignedPayload()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = string.Empty,
            ["Services:QuickBooks:OAuth:Environment"] = "sandbox"
        });
        using var client = factory.CreateClient();

        using var content = CreateJsonContent("{\"eventNotifications\":[]}");
        var response = await client.PostAsync(new Uri("/qbo/webhooks", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhooks_ProductionWithoutVerifier_ReturnsServiceUnavailable()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = string.Empty,
            ["Services:QuickBooks:OAuth:Environment"] = "production"
        }, environmentName: "Production");
        using var client = factory.CreateClient();

        using var content = CreateJsonContent("{\"eventNotifications\":[]}");
        var response = await client.PostAsync(new Uri("/qbo/webhooks", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Webhooks_WithValidSignature_ReturnsOk()
    {
        const string secret = "test-secret";
        const string payload = "{\"eventNotifications\":[{\"realmId\":\"123\"}]}";

        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = secret,
            ["Services:QuickBooks:OAuth:Environment"] = "sandbox"
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/qbo/webhooks", UriKind.Relative))
        {
            Content = CreateJsonContent(payload)
        };
        request.Headers.Add("intuit-signature", ComputeSignature(secret, payload));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhooks_WithInvalidSignature_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = "test-secret",
            ["Services:QuickBooks:OAuth:Environment"] = "sandbox"
        });
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/qbo/webhooks", UriKind.Relative))
        {
            Content = CreateJsonContent("{\"eventNotifications\":[]}")
        };
        request.Headers.Add("intuit-signature", Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static WebhooksApplicationFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        string environmentName = "Development") =>
        new(configurationOverrides, environmentName);

    private static StringContent CreateJsonContent(string payload) =>
        new(payload, Encoding.UTF8, "application/json");

    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
