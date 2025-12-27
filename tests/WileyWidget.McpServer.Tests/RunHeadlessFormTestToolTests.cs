using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.McpServer.Tests
{
    public class RunHeadlessFormTestToolTests
    {
        [Fact]
        public async Task RunHeadlessFormTestTool_TimesOut_WhenExecutionExceedsTimeout()
        {
            var code = @"Console.WriteLine(""START""); await System.Threading.Tasks.Task.Delay(2000); return null;";
            var result = await WileyWidget.McpServer.Tools.RunHeadlessFormTestTool.RunHeadlessFormTest(testCode: code, timeoutSeconds: 1);
            Assert.Contains("Test FAILED (Timeout", result);
            Assert.Contains("START", result);
        }

        [Fact]
        public async Task RunHeadlessFormTestTool_Passes_WhenWithinTimeout()
        {
            var code = @"Console.WriteLine(""START""); await System.Threading.Tasks.Task.Delay(100); return true;";
            var result = await WileyWidget.McpServer.Tools.RunHeadlessFormTestTool.RunHeadlessFormTest(testCode: code, timeoutSeconds: 5);
            Assert.Contains("✅ Test PASSED", result);
            Assert.Contains("START", result);
        }
    }
}
