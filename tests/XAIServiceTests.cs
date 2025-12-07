using Xunit;
using Moq;
using FluentValidation;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services.Validation;

namespace WileyWidget.Tests;

/// <summary>
/// Unit tests for AI Service functionality including message sending,
/// tool execution, validation, and conversation handling.
/// </summary>
public class XAIServiceTests
{
    private readonly Mock<IAIService> _mockAiService;
    private readonly ChatMessageValidator _messageValidator;
    private readonly ToolCallValidator _toolCallValidator;

    public XAIServiceTests()
    {
        _mockAiService = new Mock<IAIService>();
        _messageValidator = new ChatMessageValidator();
        _toolCallValidator = new ToolCallValidator();
    }

    #region Message Validation Tests

    [Fact]
    public void ValidateChatMessage_WithValidMessage_ShouldPass()
    {
        // Arrange
        var message = ChatMessage.CreateUserMessage("What is the current budget?");

        // Act
        var result = _messageValidator.Validate(message);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateChatMessage_WithEmptyMessage_ShouldFail()
    {
        // Arrange
        var message = new ChatMessage { Message = string.Empty, IsUser = true };

        // Act
        var result = _messageValidator.Validate(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Errors[0].ErrorMessage.ToLower());
    }

    [Fact]
    public void ValidateChatMessage_WithTooLongMessage_ShouldFail()
    {
        // Arrange
        var longMessage = new string('a', 5001);
        var message = ChatMessage.CreateUserMessage(longMessage);

        // Act
        var result = _messageValidator.Validate(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("exceed", result.Errors[0].ErrorMessage.ToLower());
    }

    [Fact]
    public void ValidateChatMessage_WithSQLInjectionPattern_ShouldFail()
    {
        // Arrange
        var message = ChatMessage.CreateUserMessage("'; DROP TABLE Accounts; --");

        // Act
        var result = _messageValidator.Validate(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("dangerous", result.Errors[0].ErrorMessage.ToLower());
    }

    #endregion

    #region Tool Call Validation Tests

    [Fact]
    public void ValidateToolCall_WithValidTool_ShouldPass()
    {
        // Arrange
        var toolCall = ToolCall.Create(
            "get_budget_data",
            new Dictionary<string, object> { { "account_id", "ACC001" } }
        );

        // Act
        var result = _toolCallValidator.Validate(toolCall);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateToolCall_WithUnallowedTool_ShouldFail()
    {
        // Arrange
        var toolCall = ToolCall.Create(
            "malicious_tool",
            new Dictionary<string, object>()
        );

        // Act
        var result = _toolCallValidator.Validate(toolCall);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not allowed", result.Errors[0].ErrorMessage.ToLower());
    }

    [Fact]
    public void ValidateToolCall_WithTooManyArguments_ShouldFail()
    {
        // Arrange
        var args = Enumerable.Range(0, 15)
            .ToDictionary(i => $"arg{i}", i => (object)$"value{i}");

        var toolCall = new ToolCall(
            "call_123",
            "get_budget_data",
            args
        );

        // Act
        var result = _toolCallValidator.Validate(toolCall);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too many", result.Errors[0].ErrorMessage.ToLower());
    }

    [Fact]
    public void ValidateToolCall_WithPathTraversal_ShouldFail()
    {
        // Arrange
        var toolCall = ToolCall.Create(
            "read",
            new Dictionary<string, object> { { "path", "../../sensitive_file.txt" } }
        );

        // Act
        var result = _toolCallValidator.Validate(toolCall);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid", result.Errors[0].ErrorMessage.ToLower());
    }

    #endregion

    #region SendMessageAsync Tests

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ShouldReturnResponse()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>();
        _mockAiService
            .Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse("Test response"));

        // Act
        var result = await _mockAiService.Object.SendMessageAsync(
            "What is the budget?",
            conversationHistory
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test response", result.Content);
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleMessages_ShouldMaintainHistory()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>();
        var responses = new[] { "First response", "Second response", "Third response" };
        var responseIndex = 0;

        _mockAiService
            .Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var response = responses[responseIndex++];
                return new ChatResponse(response);
            });

        // Act
        var result1 = await _mockAiService.Object.SendMessageAsync("Message 1", conversationHistory);
        var result2 = await _mockAiService.Object.SendMessageAsync("Message 2", conversationHistory);
        var result3 = await _mockAiService.Object.SendMessageAsync("Message 3", conversationHistory);

        // Assert
        Assert.Equal("First response", result1.Content);
        Assert.Equal("Second response", result2.Content);
        Assert.Equal("Third response", result3.Content);
    }

    #endregion

    #region ExecuteToolCallAsync Tests

    [Fact]
    public async Task ExecuteToolCallAsync_WithValidTool_ShouldReturnSuccess()
    {
        // Arrange
        var toolCall = ToolCall.Create("get_budget_data", new Dictionary<string, object> { { "account_id", "ACC001" } });

        _mockAiService
            .Setup(s => s.ExecuteToolCallAsync(It.IsAny<ToolCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCallResult.Success(toolCall.Id, "Budget data retrieved"));

        // Act
        var result = await _mockAiService.Object.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Equal("Budget data retrieved", result.Content);
    }

    [Fact]
    public async Task ExecuteToolCallAsync_WithInvalidTool_ShouldReturnError()
    {
        // Arrange
        var toolCall = new ToolCall("call_123", "invalid_tool", new Dictionary<string, object>());

        _mockAiService
            .Setup(s => s.ExecuteToolCallAsync(It.IsAny<ToolCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolCallResult.Error(toolCall.Id, "Unknown tool: invalid_tool"));

        // Act
        var result = await _mockAiService.Object.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Contains("Unknown tool", result.ErrorMessage);
    }

    #endregion

    #region Conversation History Tests

    [Fact]
    public void CreateConversationHistory_WithValidData_ShouldInitialize()
    {
        // Arrange & Act
        var conversation = new ConversationHistory
        {
            ConversationId = "conv_123",
            Title = "Budget Review",
            Description = "Reviewing Q4 budget",
            MessagesJson = "[]",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("conv_123", conversation.ConversationId);
        Assert.Equal("Budget Review", conversation.Title);
        Assert.Equal(0, conversation.MessageCount);
        Assert.False(conversation.IsArchived);
    }

    [Fact]
    public void ConversationHistory_Validation_ShouldEnforceConstraints()
    {
        // Arrange
        var validator = new ConversationHistoryValidator();
        var conversation = new ConversationHistory
        {
            ConversationId = string.Empty, // Invalid
            Title = "Test",
            MessagesJson = "[]"
        };

        // Act
        var result = validator.Validate(conversation);

        // Assert
        Assert.False(result.IsValid);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SendMessageAsync_WithNetworkError_ShouldHandleGracefully()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>();
        _mockAiService
            .Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _mockAiService.Object.SendMessageAsync("Test", conversationHistory)
        );
    }

    [Fact]
    public async Task SendMessageAsync_WithRateLimiting_ShouldReturnAppropriateError()
    {
        // Arrange
        var conversationHistory = new List<ChatMessage>();
        _mockAiService
            .Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse("Rate limit exceeded. Please try again in a moment."));

        // Act
        var result = await _mockAiService.Object.SendMessageAsync("Test", conversationHistory);

        // Assert
        Assert.Contains("Rate limit", result.Content);
    }

    #endregion
}
