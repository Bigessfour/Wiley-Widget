using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for AIAssistantService - Tool execution via Python bridge
    /// Tests tool detection, execution, error handling, and concurrency control
    /// </summary>
    public class AIAssistantServiceTests
    {
        private readonly Mock<ILogger<AIAssistantService>> _mockLogger;
        private readonly IConfiguration _configuration;
        private readonly AIAssistantService _service;

        public AIAssistantServiceTests()
        {
            _mockLogger = new Mock<ILogger<AIAssistantService>>();
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AI:PythonExecutable"] = "python",
                    ["AI:ToolExecutionTimeoutSeconds"] = "30"
                })
                .Build();
            _service = new AIAssistantService(_mockLogger.Object, _configuration);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AIAssistantService(null!, config));
        }

        [Fact]
        public void Constructor_InitializesSuccessfully_WithValidLogger()
        {
            // Arrange & Act
            var service = new AIAssistantService(_mockLogger.Object, _configuration);

            // Assert
            Assert.NotNull(service);
        }

        #endregion

        #region ParseInputForTool Tests

        [Theory]
        [InlineData("read MainForm.cs", "read_file")]
        [InlineData("READ SomeFile.txt", "read_file")]
        [InlineData("grep search pattern", "grep_search")]
        [InlineData("GREP something", "grep_search")]
        [InlineData("search for code", "semantic_search")]
        [InlineData("SEARCH query", "semantic_search")]
        [InlineData("list files", "list_directory")]
        [InlineData("LIST .", "list_directory")]
        [InlineData("get errors", "get_errors")]
        [InlineData("get error", "get_errors")]
        public void ParseInputForTool_WithValidToolCommand_ReturnsToolCall(string input, string expectedToolName)
        {
            // Act
            var result = _service.ParseInputForTool(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedToolName, result.Name);
            Assert.NotNull(result.Arguments);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("just regular text")]
        [InlineData("explain caching")]
        [InlineData("what is budget?")]
        public void ParseInputForTool_WithNonToolInput_ReturnsNull(string input)
        {
            // Act
            var result = _service.ParseInputForTool(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ParseInputForTool_WithNullInput_ReturnsNull()
        {
            // Act
            var result = _service.ParseInputForTool(null!);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region ExecuteToolAsync Tests

        [Fact]
        public async Task ExecuteToolAsync_WithNullToolCall_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _service.ExecuteToolAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task ExecuteToolAsync_WithValidToolCall_LogsExecution()
        {
            // Arrange
            var toolCall = ToolCall.Create("read_file", new Dictionary<string, object>
            {
                { "path", "test.cs" }
            });

            // Act
            // Note: This will fail if Python is not available, but tests the logging
            var result = await _service.ExecuteToolAsync(toolCall, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(toolCall.Id, result.ToolCallId);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Executing tool")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteToolAsync_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            var toolCall = ToolCall.Create("read_file", new Dictionary<string, object>
            {
                { "path", "test.cs" }
            });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await _service.ExecuteToolAsync(toolCall, cts.Token));
        }

        #endregion

        #region GetAvailableTools Tests

        [Fact]
        public void GetAvailableTools_ReturnsNonEmptyList()
        {
            // Act
            var tools = _service.GetAvailableTools();

            // Assert
            Assert.NotNull(tools);
            Assert.NotEmpty(tools);
        }

        [Fact]
        public void GetAvailableTools_ContainsExpectedTools()
        {
            // Act
            var tools = _service.GetAvailableTools();
            var toolNames = tools.Select(t => t.Name).ToList();

            // Assert
            var expectedNames = new[]
            {
                "mcp_filesystem_read_text_file",
                "mcp_filesystem_search_files",
                "mcp_filesystem_list_directory",
                "mcp_filesystem_read_multiple_files",
                "semantic_search",
                "get_errors"
            };

            foreach (var expectedName in expectedNames)
            {
                Assert.Contains(expectedName, toolNames);
            }
        }

        #endregion

        #region ValidateToolCall Tests

        [Fact]
        public void ValidateToolCall_WithNullToolCall_ReturnsFalse()
        {
            // Act
            var result = _service.ValidateToolCall(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateToolCall_WithValidToolCall_ReturnsTrue()
        {
            // Arrange
            var toolCall = ToolCall.Create("read_file", new Dictionary<string, object>
            {
                { "path", "test.cs" }
            });

            // Act
            var result = _service.ValidateToolCall(toolCall);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateToolCall_WithInvalidToolName_ReturnsFalse()
        {
            // Arrange
            var toolCall = ToolCall.Create("invalid_tool", new Dictionary<string, object>
            {
                { "param", "value" }
            });

            // Act
            var result = _service.ValidateToolCall(toolCall);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateToolCall_WithMissingArguments_ReturnsFalse()
        {
            // Arrange
            var toolCall = ToolCall.Create("read_file", new Dictionary<string, object>());

            // Act
            var result = _service.ValidateToolCall(toolCall);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task ExecuteToolAsync_WithMultipleConcurrentCalls_HandlesGracefully()
        {
            // Arrange
            var toolCalls = Enumerable.Range(1, 10).Select(i =>
                ToolCall.Create("list_directory", new Dictionary<string, object>
                {
                    { "path", "." }
                })
            ).ToList();

            // Act
            var tasks = toolCalls.Select(tc => _service.ExecuteToolAsync(tc, TestContext.Current.CancellationToken));
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(10, results.Length);
            Assert.All(results, r => Assert.NotNull(r));
        }

        #endregion
    }
}
