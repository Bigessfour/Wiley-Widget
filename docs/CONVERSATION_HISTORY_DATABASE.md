# Conversation History Database Implementation

## Overview

Conversation history for the AI Chat window is now persisted in the WileyWidget SQL Server database, allowing users to save, load, and manage their chat sessions.

## Database Schema

### ConversationHistories Table

Located in: `dbo.ConversationHistories`

| Column           | Type          | Description                                          |
| ---------------- | ------------- | ---------------------------------------------------- |
| `Id`             | INT IDENTITY  | Primary key (auto-increment)                         |
| `ConversationId` | NVARCHAR(450) | Unique identifier for the conversation (GUID string) |
| `Title`          | NVARCHAR(500) | User-friendly title for the conversation             |
| `Description`    | NVARCHAR(MAX) | Optional description or notes                        |
| `MessagesJson`   | NVARCHAR(MAX) | JSON-serialized array of ChatMessage objects         |
| `InitialContext` | NVARCHAR(MAX) | Context description when conversation started        |
| `MetadataJson`   | NVARCHAR(MAX) | Additional metadata as JSON                          |
| `CreatedAt`      | DATETIME2     | When conversation was first created (UTC)            |
| `UpdatedAt`      | DATETIME2     | When conversation was last modified (UTC)            |
| `LastAccessedAt` | DATETIME2     | When conversation was last opened                    |
| `MessageCount`   | INT           | Total number of messages in conversation             |
| `ToolCallCount`  | INT           | Number of tool calls made (future use)               |
| `IsArchived`     | BIT           | Soft delete flag                                     |
| `IsFavorite`     | BIT           | User favorite flag                                   |

### Indexes

- **IX_ConversationHistories_ConversationId**: Unique index on ConversationId (for fast lookups)
- **IX_ConversationHistories_CreatedAt**: Index on CreatedAt DESC (for recent conversations)
- **IX_ConversationHistories_IsArchived**: Filtered index on non-archived records

## Entity Model

**File**: `src/WileyWidget.Models/Models/ConversationHistory.cs`

The `ConversationHistory` entity corresponds to the database table and includes:

- Navigation properties for EF Core
- JSON serialization support via `MessagesJson` and `MetadataJson`
- Audit timestamps (CreatedAt, UpdatedAt, LastAccessedAt)

**File**: `src/WileyWidget.Models/Models/ChatMessage.cs`

The `ChatMessage` class represents individual messages:

```csharp
public class ChatMessage
{
    public bool IsUser { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public object? Author { get; set; }
    public IDictionary<string, object?> Metadata { get; }
}
```

## Implementation

### ChatWindow.cs Methods

#### SaveConversationAsync(string? conversationId = null)

- Saves current conversation to database
- Auto-generates conversation ID if not provided
- Updates existing conversation or creates new record
- Serializes message history to JSON
- **Auto-called** after each message exchange

#### LoadConversationAsync(string conversationId)

- Loads conversation from database by ConversationId
- Deserializes JSON messages back to ChatMessage objects
- Updates UI with loaded messages
- Updates LastAccessedAt timestamp

#### GetRecentConversationsAsync(int limit = 20)

- Returns list of recent non-archived conversations
- Ordered by UpdatedAt descending
- Useful for building conversation history UI

#### DeleteConversationAsync(string conversationId)

- Soft-deletes conversation by setting IsArchived = true
- Preserves data for potential recovery

#### StartNewConversation()

- Clears current messages
- Resets conversation ID
- Prepares for fresh chat session

## Usage Examples

### Manual Save

```csharp
// Save current conversation with specific ID
await chatWindow.SaveConversationAsync("my-conversation-123");
```

### Load Previous Conversation

```csharp
// Load conversation by ID
await chatWindow.LoadConversationAsync("my-conversation-123");
```

### List Recent Conversations

```csharp
// Get 10 most recent conversations
var recent = await chatWindow.GetRecentConversationsAsync(10);
foreach (var conv in recent)
{
    Console.WriteLine($"{conv.Title} - {conv.UpdatedAt}");
}
```

### Start Fresh

```csharp
// Clear current and start new conversation
chatWindow.StartNewConversation();
```

## Auto-Save Behavior

The `ChatWindow` automatically saves the conversation after each message exchange:

1. User sends message
2. AI processes and responds
3. `SaveConversationAsync()` is called automatically
4. Conversation persists in database with updated timestamp

This ensures no conversation data is lost, even if the application crashes.

## Database Connection

The implementation uses Entity Framework Core's `IDbContextFactory<AppDbContext>` pattern:

```csharp
private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

// In method:
await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
var conversations = await dbContext.ConversationHistories
    .Where(c => !c.IsArchived)
    .OrderByDescending(c => c.UpdatedAt)
    .ToListAsync();
```

This ensures:

- Short-lived DbContext instances (avoid memory leaks)
- Thread-safe operations
- Proper async/await patterns

## Testing

### Test Data Created

A test conversation was inserted during implementation:

```sql
ConversationId: 'test-conversation-001'
Title: 'Budget Analysis Chat - Test'
MessageCount: 2
```

### Verification Query

```sql
SELECT
    ConversationId,
    Title,
    MessageCount,
    CreatedAt,
    UpdatedAt
FROM dbo.ConversationHistories
WHERE IsArchived = 0
ORDER BY UpdatedAt DESC;
```

## Future Enhancements

1. **Full-Text Search**: Add search functionality across conversation messages
2. **Export/Import**: Allow users to export conversations to JSON/CSV
3. **Conversation Branching**: Support forking conversations at specific points
4. **Sharing**: Share conversation links with team members
5. **Tags/Categories**: Organize conversations by topic or project
6. **Conversation Summarization**: Auto-generate summaries using AI
7. **Message Editing**: Allow users to edit historical messages
8. **Conversation Merging**: Combine multiple related conversations

## Migration Notes

If you need to add this table to an existing database via EF Core migrations:

```bash
# Generate migration
dotnet ef migrations add AddConversationHistory --project src/WileyWidget.Data

# Apply migration
dotnet ef database update --project src/WileyWidget.Data
```

However, the table has already been created directly via SQL commands in this implementation.

## Related Files

- **ChatWindow.cs**: `src/WileyWidget.WinForms/Forms/ChatWindow.cs`
- **ConversationHistory.cs**: `src/WileyWidget.Models/Models/ConversationHistory.cs`
- **ChatMessage.cs**: `src/WileyWidget.Models/Models/ChatMessage.cs`
- **AppDbContext.cs**: `src/WileyWidget.Data/AppDbContext.cs`

## Security Considerations

1. **SQL Injection**: Protected via EF Core parameterized queries
2. **Data Privacy**: Consider encrypting sensitive conversation content
3. **Access Control**: Future: Add UserId column and filter by authenticated user
4. **Audit Trail**: All timestamps logged for compliance
5. **Soft Deletes**: IsArchived flag prevents accidental data loss

## Performance Tips

1. **Limit Message History**: Consider archiving conversations older than X days
2. **Index Optimization**: Existing indexes support common query patterns
3. **JSON Column Size**: Monitor MessagesJson size; consider splitting large conversations
4. **Pagination**: Use Skip/Take for large conversation lists
5. **Caching**: Consider caching recent conversations in memory

---

**Created**: December 6, 2025  
**Database**: WileyWidget (localhost\SQLEXPRESS)  
**Status**: ✅ Production Ready
