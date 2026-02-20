using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.WinForms.Tests.Integration;

public class TempTest
{
    private readonly ITestOutputHelper _output;

    public TempTest(ITestOutputHelper output)
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
