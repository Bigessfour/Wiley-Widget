# Grok API Refactoring - Quick Reference

## Before & After

### Endpoint
```
OLD:  POST https://api.x.ai/v1/chat/completions
NEW:  POST https://api.x.ai/v1/responses
```

### Request
```csharp
// OLD
POST /v1/chat/completions {
  "model": "grok-4",
  "messages": [...],
  "stream": false
}

// NEW
POST /v1/responses {
  "model": "grok-4",
  "input": [...],           // Changed: messages → input
  "stream": false,
  "store": true             // New: enables 30-day storage
}
```

### Response Access
```csharp
// OLD
var text = doc["choices"][0]["message"]["content"].GetString();

// NEW
var text = doc["output"][0]["content"][0]["text"].GetString();
```

---

## Most Common Operations

### Get Simple Response
```csharp
var response = await grokService.GetSimpleResponse(
    "Your question here"
);
```

### Stream Response with Callback
```csharp
await grokService.GetStreamingResponseAsync(
    "Your question here",
    onChunk: chunk => Console.Write(chunk)
);
```

### Validate API Key
```csharp
var (success, msg) = await grokService.ValidateApiKeyAsync();
```

### Retrieve Stored Response (NEW)
```csharp
var oldResponse = await grokService.GetResponseByIdAsync("response-id");
```

### Continue Conversation (NEW)
```csharp
var followUp = await grokService.ContinueConversationAsync(
    "Follow-up question",
    previousResponseId: "previous-response-id"
);
```

### Delete Response (NEW)
```csharp
await grokService.DeleteResponseAsync("response-id");
```

---

## Configuration (No Changes)
```json
{
  "Grok:ApiKey": "xai-...",
  "Grok:Model": "grok-4",
  "Grok:Endpoint": "https://api.x.ai/v1"
}
```

Or use `XAI:` prefix instead of `Grok:`

---

## What's New

| Feature | Details |
|---------|---------|
| **Response Storage** | Responses automatically stored for 30 days |
| **Response Retrieval** | `GetResponseByIdAsync(id)` - retrieve any stored response |
| **Response Deletion** | `DeleteResponseAsync(id)` - delete stored responses |
| **Conversation Continuation** | `ContinueConversationAsync(message, previousId)` - continue without context |
| **Better Tracking** | Response IDs automatically tracked in `_conversationResponseIds` |

---

## Migration Checklist

- [ ] Code uses `GetSimpleResponse()` or `GetStreamingResponseAsync()` (not manual parsing)
- [ ] Tests updated if mocking responses
- [ ] Configuration keys verified (Grok: or XAI: both work)
- [ ] Error handling reviewed
- [ ] Logging enabled for debugging

---

## Key Differences at a Glance

| Feature | Old | New |
|---------|-----|-----|
| Endpoint | `/chat/completions` | `/responses` |
| Request field | `messages` | `input` |
| Response location | `choices[0].message.content` | `output[0].content[0].text` |
| Storage | None | 30 days |
| Retrieval | Not possible | `GetResponseByIdAsync()` |
| Deletion | Not applicable | `DeleteResponseAsync()` |
| Continuation | Manual (send full context) | Automatic with `previous_response_id` |

---

## Error Messages (Same as Before)
- "No API key configured for Grok" → Set `Grok:ApiKey` in config
- "Model X does not exist" → Automatically retries with `grok-4`
- "HTTP 429" → Rate limited, will retry with exponential backoff
- "HTTP 401" → Invalid API key

---

## Response ID Format
```json
{
  "id": "ad5663da-63e6-86c6-e0be-ff15effa8357",
  "object": "response",
  "output": [{"content": [{"text": "..."}]}],
  "status": "completed"
}
```

Save the `id` field to retrieve/delete later.

---

## Troubleshooting

### "No content returned"
→ Verify API key is set and valid
→ Check model is available (use `ListAvailableModelsAsync()`)

### "Unexpected response structure"
→ Don't parse manually; use `GetSimpleResponse()` or `GetStreamingResponseAsync()`

### "Response not found"
→ Response may be older than 30 days
→ ID may be incorrect

### "Streaming stops abruptly"
→ Check network connectivity
→ Verify cancellation token not cancelled
→ Enable debug logging

---

## Quick Test
```csharp
// Does it work?
var response = await grokService.GetSimpleResponse("Say hello");
Console.WriteLine(response);

// Expected: Some greeting text
// Not expected: Empty, null, or technical error
```

---

## Important Notes

✅ **What Still Works**
- All public method signatures unchanged
- Configuration keys unchanged
- Error handling identical
- Logging similar (with new endpoint reference)

⚠️ **What Changed**
- Response structure (nested arrays)
- Request format (input instead of messages)
- New capabilities available (retrieval, deletion, continuation)

🔴 **Dont Do**
- ❌ Don't parse responses manually (use the wrapper methods)
- ❌ Don't assume `/chat/completions` still works (it's deprecated)
- ❌ Don't ignore response IDs (they enable new features)

🟢 **Do Do**
- ✅ Use `GetSimpleResponse()` for simple queries
- ✅ Use `GetStreamingResponseAsync()` for real-time responses
- ✅ Use `ContinueConversationAsync()` for multi-turn chats
- ✅ Track response IDs for retrieval/deletion

---

## Reference Docs
- Full guide: `docs/GROK_API_REFACTORING_GUIDE.md`
- Status report: `docs/GROK_REFACTORING_COMPLETION_REPORT.md`
- Source: `src/WileyWidget.WinForms/Services/AI/GrokAgentService.cs`

---

**Last Updated**: January 21, 2026
**Status**: ✅ Complete & Tested
**Questions**: See full refactoring guide
