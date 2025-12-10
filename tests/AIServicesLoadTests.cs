using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.Services;

namespace WileyWidget.Tests;

/// <summary>
/// Load and stress tests for AI services to ensure scalability under concurrent load.
/// Tests concurrent tool execution, message processing, and error handling under stress.
/// </summary>
public class AIServicesLoadTests
{
    private readonly Mock<IAIService> _mockAiService;
    private readonly Mock<ITelemetryService> _mockTelemetryService;

    public AIServicesLoadTests()
    {
        _mockAiService = new Mock<IAIService>();
        _mockTelemetryService = new Mock<ITelemetryService>();
    }

    [Fact]
    public async Task XAIService_ConcurrentToolCalls_ShouldHandleLoad()
    {
        // Arrange
        var xaiService = new XAIService(_mockAiService.Object, _mockTelemetryService.Object);
        var concurrentRequests = 50;
        var toolCalls = new List<Task<AIResponse>>();

        _mockAiService.Setup(x => x.SendMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIResponse { Success = true, Message = "Test response" });

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var message = ChatMessage.CreateUserMessage($"Test message {i}");
            toolCalls.Add(xaiService.SendMessageAsync(message));
        }

        var results = await Task.WhenAll(toolCalls);

        // Assert
        results.Should().HaveCount(concurrentRequests);
        results.All(r => r.Success).Should().BeTrue();
        results.All(r => r.Message.Contains("Test response")).Should().BeTrue();
    }

    [Fact]
    public async Task AIAssistantService_ConcurrentAnalysisRequests_ShouldNotFail()
    {
        // Arrange
        var mockBudgetRepo = new Mock<IBudgetRepository>();
        var mockLogger = new Mock<ILogger<AIAssistantService>>();
        var service = new AIAssistantService(mockBudgetRepo.Object, mockLogger.Object);

        mockBudgetRepo.Setup(x => x.GetByFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<BudgetEntry>
            {
                new BudgetEntry { Id = 1, FiscalYear = 2024, BudgetedAmount = 1000, ActualSpent = 800 }
            });

        var concurrentRequests = 20;
        var tasks = new List<Task<string>>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(service.AnalyzeBudgetTrendsAsync(2024));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentRequests);
        results.All(r => !string.IsNullOrEmpty(r)).Should().BeTrue();
    }

    [Fact]
    public async Task GrokSupercomputer_ConcurrentQueries_ShouldMaintainPerformance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GrokSupercomputer>>();
        var service = new GrokSupercomputer(mockLogger.Object);
        var concurrentQueries = 10;
        var queries = new List<Task<AIResponse>>();

        // Act
        for (int i = 0; i < concurrentQueries; i++)
        {
            var query = $"Analyze budget data set {i}";
            queries.Add(service.ProcessQueryAsync(query));
        }

        var startTime = DateTime.UtcNow;
        var results = await Task.WhenAll(queries);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        results.Should().HaveCount(concurrentQueries);
        results.All(r => r != null).Should().BeTrue();
        // Should complete within reasonable time (adjust based on actual performance)
        duration.TotalSeconds.Should().BeLessThan(30);
    }

    [Fact]
    public async Task AIServices_ErrorHandlingUnderLoad_ShouldNotCrash()
    {
        // Arrange
        var xaiService = new XAIService(_mockAiService.Object, _mockTelemetryService.Object);
        var errorCount = 0;

        _mockAiService.Setup(x => x.SendMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                errorCount++;
                if (errorCount % 5 == 0) // Every 5th call fails
                    throw new Exception("Simulated API error");
                return new AIResponse { Success = true, Message = "Success" };
            });

        var requests = 25;
        var tasks = new List<Task<AIResponse>>();

        // Act
        for (int i = 0; i < requests; i++)
        {
            var message = ChatMessage.CreateUserMessage($"Message {i}");
            tasks.Add(xaiService.SendMessageAsync(message));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(requests);
        // Some should succeed, some should fail gracefully
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);
        successCount.Should().BeGreaterThan(0);
        failureCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConversationHistory_LoadTest_ShouldHandleLargeHistory()
    {
        // Arrange
        var mockRepo = new Mock<IConversationRepository>();
        var mockLogger = new Mock<ILogger<ConversationHistoryService>>();
        var service = new ConversationHistoryService(mockRepo.Object, mockLogger.Object);

        var largeHistory = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Load Test Conversation",
            Messages = new List<ChatMessage>()
        };

        // Create 1000 messages
        for (int i = 0; i < 1000; i++)
        {
            largeHistory.Messages.Add(ChatMessage.CreateUserMessage($"Message {i}"));
            largeHistory.Messages.Add(ChatMessage.CreateAssistantMessage($"Response {i}"));
        }

        mockRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(largeHistory);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await service.GetConversationAsync(largeHistory.Id);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        result.Should().NotBeNull();
        result!.Messages.Should().HaveCount(2000);
        // Should complete within reasonable time
        duration.TotalMilliseconds.Should().BeLessThan(500);
    }
}
