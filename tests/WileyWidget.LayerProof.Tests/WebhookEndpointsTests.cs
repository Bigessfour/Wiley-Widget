using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WileyWidget.LayerProof.Tests;

public sealed class WebhookEndpointsTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("ok");
    }

    [Fact]
    public async Task WebhookEndpoint_AcceptsValidSignature()
    {
        const string secret = "diagnostic-secret";
        const string body = "{\"eventNotifications\":[]}";

        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = secret,
        });
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.Add("intuit-signature", ComputeSignature(secret, body));

        var response = await client.PostAsync(new Uri("/qbo/webhooks", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebhookEndpoint_RejectsMissingSignatureWhenVerifierIsConfigured()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:Webhooks:VerifierToken"] = "diagnostic-secret",
        });
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync(new Uri("/qbo/webhooks", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WebhookEndpoint_RequiresVerifierTokenInProduction()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Services:QuickBooks:OAuth:Environment"] = "production",
            ["Services:QuickBooks:Webhooks:VerifierToken"] = string.Empty,
        });
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync(new Uri("/qbo/webhooks", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static WebApplicationFactory<global::Program> CreateFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    if (settings is not null)
                    {
                        configuration.AddInMemoryCollection(settings);
                    }
                });
            });
    }

    private static string ComputeSignature(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }
}