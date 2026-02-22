using FluentAssertions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public sealed class HelloWorldIntegrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public void MainForm_ResolvesWithTestProvider()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);

        form.Should().NotBeNull();
        form.ServiceProvider.Should().NotBeNull();
    }
}
