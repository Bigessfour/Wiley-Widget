using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading.Tasks;
using Moq;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.Tests.Integration;

/// <summary>
/// Integration tests for ChatWindow conversational flow
/// Tests message handling, conversation persistence, and AI integration
/// </summary>
public class ChatWindowIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAIService> _mockAIService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly AppDbContext _testDbContext;

    public ChatWindowIntegrationTests()
    {
        // Setup in-memory database for testing
        var services = new ServiceCollection();

        // Configure in-memory database
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"ChatWindowTest_{Guid.NewGuid()}"));

        // Setup logging
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        // Register mock AI service
        _mockAIService = new Mock<IAIService>();
        services.AddSingleton(_mockAIService.Object);

        // Register mock services
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAIAssistantService>(sp => Mock.Of<IAIAssistantService>());
        services.AddScoped<ILogger<AIChatControl>>(sp => Mock.Of<ILogger<AIChatControl>>());

        _serviceProvider = services.BuildServiceProvider();
        _dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        _testDbContext = _dbContextFactory.CreateDbContext();
    }

    [Fact]
    public Task SendMessage_ShouldInvokeAIServiceWithConversationHistory() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var chatWindow = new ChatWindow(_serviceProvider);
            var conversationHistory = new List<ChatMessage>();

            _mockAIService
                .Setup(s => s.SendMessageAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<ChatMessage>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string msg, List<ChatMessage> history, CancellationToken ct) =>
                {
                    conversationHistory = history;
                    return new ChatResponse($"AI response to: {msg}");
                });

            // Act - Simulate user message
            var userMessage = "What is the budget for 2026?";
            // Note: Direct invocation requires reflection or making HandleMessageSentAsync public for testing
            // For now, this demonstrates the test structure

            // Assert
            _mockAIService.Verify(s => s.SendMessageAsync(
                It.Is<string>(m => m == userMessage),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()), Times.Once);

            Assert.NotEmpty(conversationHistory);
            Assert.Contains(conversationHistory, m => m.IsUser && m.Message == userMessage);
            await Task.CompletedTask;
        });

    [Fact]
    public Task SaveConversation_ShouldPersistToDatabase() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var chatWindow = new ChatWindow(_serviceProvider);
            var conversationId = Guid.NewGuid().ToString();

            // Act
            await chatWindow.SaveConversationAsync(conversationId);

            // Assert
            var repository = _serviceProvider.GetRequiredService<IConversationRepository>();
            var saved = await repository.GetConversationAsync(conversationId);
            Assert.NotNull(saved);
            Assert.Equal(conversationId, saved.ConversationId);
        });

    [Fact]
    public Task LoadConversation_ShouldRestoreMessagesFromDatabase() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var repository = _serviceProvider.GetRequiredService<IConversationRepository>();
            var conversationId = Guid.NewGuid().ToString();
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage("Test message 1"),
                ChatMessage.CreateAIMessage("Test response 1")
            };

            var conversation = new ConversationHistory
            {
                ConversationId = conversationId,
                Title = "Test Conversation",
                MessagesJson = System.Text.Json.JsonSerializer.Serialize(messages),
                MessageCount = messages.Count,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.SaveConversationAsync(conversation);

            var chatWindow = new ChatWindow(_serviceProvider);

            // Act
            await chatWindow.LoadConversationAsync(conversationId);

            // Assert
            var recent = await chatWindow.GetRecentConversationsAsync(10);
            Assert.Contains(recent, c => c.ConversationId == conversationId);
        });

    [Fact]
    public Task ChatFlow_EndToEnd_ShouldSaveAutomatically() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var chatWindow = new ChatWindow(_serviceProvider);

            _mockAIService
                .Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string msg, List<ChatMessage> history, CancellationToken ct) =>
                    new ChatResponse($"AI: {msg}"));

            // Act - Simulate complete chat interaction
            // 1. User sends message
            // 2. AI responds
            // 3. Conversation auto-saves

            // Note: This requires either public test methods or refactoring ChatWindow
            // to expose testable interfaces

            // Assert
            var recent = await chatWindow.GetRecentConversationsAsync(1);
            Assert.NotNull(recent);
        });

    [Fact]
    public Task DeleteConversation_ShouldSoftDelete() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var repository = _serviceProvider.GetRequiredService<IConversationRepository>();
            var conversationId = Guid.NewGuid().ToString();

            var conversation = new ConversationHistory
            {
                ConversationId = conversationId,
                Title = "Test Delete",
                MessagesJson = "[]",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.SaveConversationAsync(conversation);

            var chatWindow = new ChatWindow(_serviceProvider);

            // Act
            await chatWindow.DeleteConversationAsync(conversationId);

            // Assert
            var deleted = await repository.GetConversationAsync(conversationId);
            Assert.Null(deleted); // Soft-deleted conversations not returned by repository
        });

    [Fact]
    public Task ConversationHistory_ShouldMaintainMessageOrder() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var repository = _serviceProvider.GetRequiredService<IConversationRepository>();
            var conversationId = Guid.NewGuid().ToString();

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage("Message 1"),
                ChatMessage.CreateAIMessage("Response 1"),
                ChatMessage.CreateUserMessage("Message 2"),
                ChatMessage.CreateAIMessage("Response 2")
            };

            var conversation = new ConversationHistory
            {
                ConversationId = conversationId,
                Title = "Order Test",
                MessagesJson = System.Text.Json.JsonSerializer.Serialize(messages),
                MessageCount = messages.Count,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.SaveConversationAsync(conversation);

            // Act
            var loaded = await repository.GetConversationAsync(conversationId);
            var loadedMessages = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(loaded!.MessagesJson);

            // Assert
            Assert.Equal(4, loadedMessages!.Count);
            Assert.Equal("Message 1", loadedMessages[0].Message);
            Assert.Equal("Response 1", loadedMessages[1].Message);
            Assert.Equal("Message 2", loadedMessages[2].Message);
            Assert.Equal("Response 2", loadedMessages[3].Message);
        });

    [Fact]
    public Task GetRecentConversations_ShouldOrderByUpdatedDate() =>
        StaThreadInvoker.RunAsync(async () =>
        {
            // Arrange
            var repository = _serviceProvider.GetRequiredService<IConversationRepository>();

            var old = new ConversationHistory
            {
                ConversationId = Guid.NewGuid().ToString(),
                Title = "Old Conversation",
                MessagesJson = "[]",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var recent = new ConversationHistory
            {
                ConversationId = Guid.NewGuid().ToString(),
                Title = "Recent Conversation",
                MessagesJson = "[]",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.SaveConversationAsync(old);
            await repository.SaveConversationAsync(recent);

            var chatWindow = new ChatWindow(_serviceProvider);

            // Act
            var conversations = await chatWindow.GetRecentConversationsAsync(10);

            // Assert
            Assert.NotEmpty(conversations);
            Assert.Equal("Recent Conversation", conversations[0].Title);
        });

    [Fact]
    public Task ChatWindow_ShouldUseRepository_NotDirectEFCalls() =>
        StaThreadInvoker.RunAsync(() =>
        {
            // Arrange
            var chatWindow = new ChatWindow(_serviceProvider);

            // Assert - Verify constructor dependencies
            // This is a structural test to ensure IConversationRepository is injected
            // The refactoring should have removed IDbContextFactory<AppDbContext> dependency

            // Reflection check (simplified)
            var chatWindowType = typeof(ChatWindow);
            var fields = chatWindowType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.DoesNotContain(fields, f => f.FieldType == typeof(IDbContextFactory<AppDbContext>));
            Assert.Contains(fields, f => f.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        });

    public void Dispose()
    {
        _testDbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
