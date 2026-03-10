using System.Collections.Generic;
using Xunit;
using WileyWidget.WinForms.Services.AI.XAI;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI
{
    /// <summary>
    /// Regression tests for xAI built-in tool payload shapes used by /v1/responses.
    /// </summary>
    public sealed class XAIToolDefinitionsTests
    {
        [Fact]
        public void CreateToolDefinitionsForResponses_WhenCodeExecutionEnabled_UsesBuiltInToolType()
        {
            XAIBuiltInTools.XAIToolConfiguration config = new XAIBuiltInTools.XAIToolConfiguration
            {
                Enabled = true,
                CodeExecution = new XAIBuiltInTools.CodeExecutionConfig
                {
                    Enabled = true,
                    TimeoutSeconds = 30
                }
            };

            List<object> tools = XAIBuiltInTools.CreateToolDefinitionsForResponses(config);

            Assert.Single(tools);
            Dictionary<string, object> tool = Assert.IsType<Dictionary<string, object>>(tools[0]);

            Assert.Equal("code_interpreter", tool["type"]);
            Assert.False(tool.ContainsKey("name"));
            Assert.False(tool.ContainsKey("function"));
        }

        [Fact]
        public void CreateToolDefinitionsForResponses_WhenMultipleToolsEnabled_UsesBuiltInToolTypes()
        {
            XAIBuiltInTools.XAIToolConfiguration config = new XAIBuiltInTools.XAIToolConfiguration
            {
                Enabled = true,
                WebSearch = new XAIBuiltInTools.WebSearchConfig { Enabled = true },
                XSearch = new XAIBuiltInTools.XSearchConfig { Enabled = true },
                CodeExecution = new XAIBuiltInTools.CodeExecutionConfig { Enabled = true },
                CollectionsSearch = new XAIBuiltInTools.CollectionsSearchConfig { Enabled = true }
            };

            List<object> tools = XAIBuiltInTools.CreateToolDefinitionsForResponses(config);

            Assert.NotEmpty(tools);

            foreach (object toolObj in tools)
            {
                Dictionary<string, object> tool = Assert.IsType<Dictionary<string, object>>(toolObj);
                Assert.True(tool.ContainsKey("type"));
                Assert.Contains(tool["type"], new object[] { "web_search", "x_search", "code_interpreter", "file_search" });
                Assert.False(tool.ContainsKey("function"));
            }
        }

        [Fact]
        public void CreateToolDefinitions_WhenCodeExecutionEnabled_UsesNestedFunctionShape()
        {
            XAIBuiltInTools.XAIToolConfiguration config = new XAIBuiltInTools.XAIToolConfiguration
            {
                Enabled = true,
                CodeExecution = new XAIBuiltInTools.CodeExecutionConfig
                {
                    Enabled = true,
                    TimeoutSeconds = 30
                }
            };

            List<object> tools = XAIBuiltInTools.CreateToolDefinitions(config);

            Assert.Single(tools);
            Dictionary<string, object> tool = Assert.IsType<Dictionary<string, object>>(tools[0]);
            Dictionary<string, object> functionPayload = Assert.IsType<Dictionary<string, object>>(tool["function"]);

            Assert.Equal("function", tool["type"]);
            Assert.Equal("code_interpreter", functionPayload["name"]);
        }
    }
}
