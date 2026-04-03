using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WileyWidget.Webhooks.Tests;

internal sealed class WebhooksApplicationFactory(
    IReadOnlyDictionary<string, string?>? configurationOverrides = null,
    string environmentName = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        if (configurationOverrides is null || configurationOverrides.Count == 0)
        {
            return;
        }

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(configurationOverrides);
        });
    }
}
