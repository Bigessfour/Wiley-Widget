using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WileyWidget.WinForms;

namespace WileyWidget.WinForms.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ProgramStartupIntegrationTests
{
    [Fact]
    public void CreateFallbackServiceProvider_ProvidesConfiguration()
    {
        using var provider = (ServiceProvider)Program.CreateFallbackServiceProvider();

        var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(provider);

        configuration.Should().NotBeNull();
    }
}
