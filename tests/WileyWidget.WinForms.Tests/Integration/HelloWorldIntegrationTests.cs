using FluentAssertions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class HelloWorldIntegrationTests
{
    [Fact]
    public void MainForm_ResolvesWithTestProvider()
    {
        using var provider = IntegrationTestServices.BuildProvider();
        using var form = IntegrationTestServices.CreateMainForm(provider);

        form.Should().NotBeNull();
        form.ServiceProvider.Should().Be(provider);
    }
}
