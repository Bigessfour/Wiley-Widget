using System.Threading.Tasks;
using FluentAssertions;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests;

public class IAIServiceTests
{
    [Fact]
    public async Task NullAIService_GetInsights_ReturnsStubText()
    {
        var svc = new NullAIService();
        var res = await svc.GetInsightsAsync("ctx","q");
        res.Should().Contain("Dev Stub");
    }

    [Fact]
    public async Task NullAIService_AnalyzeData_ReturnsStubText()
    {
        var svc = new NullAIService();
        var res = await svc.AnalyzeDataAsync("d","type");
        res.Should().Contain("Dev Stub");
    }
}
