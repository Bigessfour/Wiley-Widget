# AI Chat System - Complete File Listing

This document provides a comprehensive listing of all files that interact to enable AI chat functionality in the WileyWidget application.

## Core AI Chat UI Components

### 1. Chat Window & Controls

- **`src/WileyWidget.WinForms/Forms/ChatWindow.cs`**
  - Main chat window form
  - Manages conversation history and context
  - Handles save/load of conversations to database
  - Auto-saves after each message exchange
  - Methods: `HandleMessageSentAsync`, `SetContext`, `LoadConversationAsync`, `SaveConversationAsync`, `GetRecentConversationsAsync`, `DeleteConversationAsync`, `StartNewConversation`

- **`src/WileyWidget.WinForms/Controls/AIChatControl.cs`**
  - Primary chat UI control (messages display, input box, send button)
  - Handles tool detection and execution
  - Manages conversational AI fallback
  - Observable message collection
  - Progress indication during processing
  - Methods: `SendMessageAsync`, `ParseInputForTool`, `ExecuteToolAsync`, `NotifyProcessingCompleted`, `AppendMessageToDisplay`, `TrimMessagesIfNeeded`

- **`src/WileyWidget.WinForms/Forms/MainForm.cs`**
  - Hosts the AI chat control
  - Resolves dependencies from DI container
  - Initializes `AIChatControl` with required services
  - Line 399-417: AI chat initialization

## Service Interfaces

### 2. AI Service Abstractions

- **`src/WileyWidget.Services.Abstractions/IAIService.cs`**
  - Interface for conversational AI (xAI/Grok integration)
  - Methods:
    - `SendMessageAsync(string message, List<ChatMessage>? conversationHistory = null)`
    - `GetInsightsAsync(string context, string question, CancellationToken cancellationToken)`
    - `ExecuteToolCallAsync(ToolCall toolCall)`

- **`src/WileyWidget.Services.Abstractions/IAIAssistantService.cs`**
  - Interface for tool-based AI assistant (file operations, semantic search)
  - Methods:
    - `ParseInputForTool(string input)`
    - `ExecuteToolAsync(ToolCall toolCall)`
    - `GetAvailableTools()`
    - `ValidateToolCall(ToolCall toolCall)`

## Service Implementations

### 3. AI Service Implementations

- **`src/WileyWidget.Services/XAIService.cs`**
  - Implements `IAIService`
  - Conversational AI via xAI Grok API
  - Polly resilience policies (rate limiting, circuit breaker, retry)
  - HTTP client for API communication
  - Response caching with `IMemoryCache`
  - Request/response logging and metrics
  - Methods:
    - `SendMessageAsync`: Send chat messages with conversation history
    - `GetInsightsAsync`: Get AI insights for specific questions
    - `ExecuteToolCallAsync`: Execute tool calls via AI
    - `BuildConversationMessages`: Build API payload from history
    - `SanitizeInput`: Input validation and sanitization
  - Configuration: `XAIServiceOptions` (API key, model, timeout)

- **`src/WileyWidget.Services/AIAssistantService.cs`**
  - Implements `IAIAssistantService`
  - Tool-based assistant for workspace operations
  - Python bridge for tool execution (`xai_tool_executor.py`)
  - Subprocess management with timeout
  - Semaphore for concurrency control
  - Methods:
    - `ParseInputForTool`: Regex-based tool detection
    - `ExecuteToolAsync`: Execute via Python subprocess
    - `GetAvailableTools`: List available tools
    - `ValidateToolCall`: Validate tool parameters

- **`src/WileyWidget.Services/NullAIService.cs`**
  - Null object pattern implementation of `IAIService`
  - Used in tests and when AI service unavailable
  - Returns predictable stub responses

## Models & Data Transfer Objects

### 4. Data Models

- **`src/WileyWidget.Models/Models/ChatMessage.cs`**
  - Represents a single chat message
  - Properties: `IsUser`, `Message`, `Text`, `Timestamp`, `Author`, `Metadata`
  - Factory methods: `CreateUserMessage`, `CreateAIMessage`

- **`src/WileyWidget.Models/Models/ToolCall.cs`**
  - Represents a tool invocation request
  - Properties: `Id`, `Name`, `ToolType`, `Arguments`, `Metadata`
  - Used by `IAIAssistantService`

- **`src/WileyWidget.Models/Models/ConversationHistory.cs`**
  - Entity model for persisted conversations
  - Properties: `Id`, `ConversationId`, `Title`, `MessagesJson`, `InitialContext`, `MetadataJson`, `CreatedAt`, `UpdatedAt`, `LastAccessedAt`, `MessageCount`, `ToolCallCount`, `IsArchived`, `IsFavorite`
  - Includes nested `ChatMessageData` DTO for JSON serialization

## Database Layer

### 5. Entity Framework & Database

- **`src/WileyWidget.Data/AppDbContext.cs`**
  - EF Core DbContext
  - Includes `DbSet<ConversationHistory>` for chat persistence
  - Line 73: ConversationHistories DbSet

- **Database: `dbo.ConversationHistories` Table** (WileyWidget database)
  - Schema: Id (INT), ConversationId (NVARCHAR), Title, MessagesJson, CreatedAt, UpdatedAt, etc.
  - Indexes: IX_ConversationHistories_ConversationId, IX_ConversationHistories_CreatedAt, IX_ConversationHistories_IsArchived

## Dependency Injection & Configuration

### 6. Service Registration

- **`src/WileyWidget.WinForms/Configuration/DependencyInjection.cs`**
  - Registers all AI services in DI container
  - Service registrations:
    - `services.AddScoped<IAIAssistantService, AIAssistantService>()`
    - `services.AddScoped<IAIService, XAIService>()`
    - `services.AddDbContextFactory<AppDbContext>()`
  - Configuration binding for `XAIServiceOptions`

## Validation

### 7. FluentValidation Validators

- **`src/WileyWidget.Services/Validation/AIServiceValidators.cs`**
  - `ChatMessageValidator`: Validates chat messages
  - `ToolCallValidator`: Validates tool call parameters
  - `ConversationHistoryValidator`: Validates conversation entities

## External Scripts

### 8. Python Bridge (Tool Executor)

- **`scripts/xai_tool_executor.py`** (if exists)
  - Executes filesystem and semantic search tools
  - Called by `AIAssistantService` via subprocess
  - Tools: read, grep, list, search, get_errors

## Supporting Services

### 9. Supporting Infrastructure

- **`src/WileyWidget.Services/MemoryCacheService.cs`**
  - Caching service used by `XAIService`
  - Response caching to reduce API calls

- **`src/WileyWidget.Services/LocalSecretVaultService.cs`**
  - Manages API keys (xAI API key)
  - Secure storage and retrieval

- **`src/WileyWidget.Services.Abstractions/ICacheService.cs`**
  - Cache service interface

- **`src/WileyWidget.Services.Abstractions/ISecretVaultService.cs`**
  - Secret management interface

## Test Files

### 10. Test Suite

- **`tests/XAIServiceTests.cs`**
  - Unit tests for `XAIService`
  - Tests conversation history, tool calls, rate limiting

- **`tests/AI_Services_Integration_Verification.cs`**
  - Integration tests and architecture documentation
  - Verifies tool detection, execution, fallback logic
  - Contains comprehensive flow diagrams

- **`tests/AIServices_Audit_Duplicates.cs`**
  - Audit tests to ensure no duplicate AI services
  - Verifies proper interface implementation

- **`tests/AIChatControl_Integration_Analysis.cs`** (if exists)
  - Integration tests for `AIChatControl`

- **`tests/AIChatControl_SendMessageAsync_Tests.cs`** (if exists)
  - Unit tests for message sending logic

- **`tests/WileyWidget.Tests/TestInfrastructure/Doubles/NullAIServiceDouble.cs`**
  - Test double for `IAIService`
  - Used in integration tests

## Documentation

### 11. Documentation Files

- **`docs/CONVERSATION_HISTORY_DATABASE.md`**
  - Database schema documentation
  - Usage examples for save/load
  - Performance and security considerations

- **`docs/CONVERSATION_HISTORY_SUMMARY.md`**
  - Implementation summary
  - Completed tasks and features
  - Production readiness checklist

- **`docs/AICHAT_INTEGRATION_SUMMARY.md`** (if exists)
  - Overall AI chat integration documentation

## Configuration Files

### 12. Application Configuration

- **`appsettings.json`** / **`appsettings.Development.json`**
  - XAI service configuration
  - API endpoints, timeouts, model selection
  - Logging configuration

- **`secrets/xai_api_key.txt`** or environment variable `XAI_API_KEY`
  - xAI API key for Grok integration

## Architecture Flow

### Message Processing Flow

```
User Input → AIChatControl.SendMessageAsync()
    ├─→ Tool Detected? → AIAssistantService.ExecuteToolAsync()
    │       └─→ Python subprocess → Tool result
    │
    └─→ No Tool? → XAIService.GetInsightsAsync()
            └─→ xAI API → Conversational response

Result → ChatMessage added to ObservableCollection
      → Auto-save to database (ChatWindow.SaveConversationAsync)
      → UI updated (AppendMessageToDisplay)
```

### Service Dependencies

```
ChatWindow
    ├─→ IServiceProvider
    ├─→ ILogger<ChatWindow>
    ├─→ IAIService (XAIService)
    └─→ IDbContextFactory<AppDbContext>

AIChatControl
    ├─→ IAIAssistantService (AIAssistantService)
    ├─→ ILogger<AIChatControl>
    └─→ IAIService (XAIService) - optional fallback

XAIService
    ├─→ HttpClient
    ├─→ IMemoryCache (MemoryCacheService)
    ├─→ ILogger<XAIService>
    ├─→ IOptions<XAIServiceOptions>
    └─→ ISecretVaultService (for API key)

AIAssistantService
    ├─→ ILogger<AIAssistantService>
    └─→ Python subprocess bridge
```

## Key Integration Points

1. **MainForm.cs Line 399-417**: Initializes `AIChatControl` with dependencies
2. **DependencyInjection.cs**: Registers `IAIService`, `IAIAssistantService`, `IDbContextFactory`
3. **ChatWindow.HandleMessageSentAsync**: Delegates to `IAIService.SendMessageAsync`, then auto-saves
4. **AIChatControl.SendMessageAsync**: Coordinates tool detection, execution, and conversational fallback
5. **AppDbContext Line 73**: Includes `ConversationHistories` DbSet for persistence

## Environment Variables & Secrets

- `XAI_API_KEY`: xAI Grok API key (stored in secret vault)
- Connection string for WileyWidget database (SQL Server Express)

---

**Total Files in AI Chat System**: ~30+ files (code + tests + docs)

**Core Runtime Files**: 15 files

- 2 UI files (ChatWindow, AIChatControl)
- 2 service interfaces
- 2 service implementations
- 3 model classes
- 1 DbContext
- 1 DI configuration
- 3 validators
- 1 MainForm integration

**Status**: ✅ Fully Integrated and Production Ready
