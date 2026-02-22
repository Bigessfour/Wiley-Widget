using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.WinForms.Tests.Integration;

[Collection("IntegrationTests")]
public class TempTest : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public TempTest(ITestOutputHelper output, IntegrationTestFixture fixture) : base(fixture)
    {
        _output = output;
    }

    [Fact]
    public void Temp_Test_Method()
    {
        _output.WriteLine("Temp test running!");
        Assert.True(true);
    }
}
