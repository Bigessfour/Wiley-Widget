# Grok API Refactoring Guide - Migration from /v1/chat/completions to /v1/responses

## Overview

The xAI Grok endpoint has transitioned from the legacy `/v1/chat/completions` endpoint to the new **Responses API** at `/v1/responses`. This guide documents the complete refactoring of `GrokAgentService` to support the new endpoint while maintaining backward compatibility where appropriate.

**Status**: ✅ Complete - GrokAgentService fully migrated to `/v1/responses` endpoint

---

## Key Changes

### 1. **Endpoint Migration**

| Aspect                        | Old (/chat/completions)      | New (/responses)                |
| ------------------------------ | ----------------------------- | -------------------------------- |
| **Endpoint**                  | `POST /v1/chat/completions`  | `POST /v1/responses`            |
| **Request field**             | `messages` (array)           | `input` (array)                 |
| **Response structure**        | `choices[0].message.content` | `output[0].content[0].text`     |
| **Storage**                   | Not available                | 30-day retention                |
| **Conversation continuation** | Not available                | Via `previous_response_id`      |
| **Response deletion**         | Not available                | Via `DELETE /v1/responses/{id}` |
| **Response retrieval**        | Not available                | Via `GET /v1/responses/{id}`    |

### 2. **Request Format Comparison**

**Old Format (/chat/completions):**

```json
{
  "model": "grok-4",
  "messages": [
    { "role": "system", "content": "You are helpful." },
    { "role": "user", "content": "Hello!" }
  ],
  "stream": false,
  "temperature": 0.7
}
```

**New Format (/responses):**

```json
{
  "model": "grok-4",
  "input": [
    { "role": "system", "content": "You are helpful." },
    { "role": "user", "content": "Hello!" }
  ],
  "stream": false,
  "store": true,
  "temperature": 0.7
}
```

### 3. **Response Format Comparison**

**Old Format (/chat/completions):**

```json
{
  "id": "...",
  "object": "chat.completion",
  "choices": [{
    "message": {
      "role": "assistant",
      "content": "Hello there!"
    }
  }],
  "usage": {...}
}
```

**New Format (/responses):**

```json
{
  "id": "ad5663da-...",
  "object": "response",
  "output": [{
    "content": [{
      "type": "output_text",
      "text": "Hello there!"
    }],
    "role": "assistant"
  }],
  "status": "completed",
  "store": true,
  "usage": {...}
}
```

---

## Refactored Methods

### Core HTTP Methods

#### 1. **GetSimpleResponse** (Non-streaming)

- **Before**: Posted to `/v1/chat/completions` with `messages` array
- **After**: Posts to `/v1/responses` with `input` array
- **Response parsing**: Changed from `choices[0].message.content` to `output[0].content[0].text`

```csharp
// New usage
var response = await grokService.GetSimpleResponse(
    "Tell me a joke",
    systemPrompt: "You are a comedian",
    modelOverride: "grok-4"
);
```

#### 2. **GetStreamingResponseAsync** (Streaming with SSE)

- **Before**: Streamed from `/v1/chat/completions` with `delta.content`
- **After**: Streams from `/v1/responses` with `output[0].content[0].text`
- **Response tracking**: Now captures and stores response IDs for conversation continuation

```csharp
// New usage
var response = await grokService.GetStreamingResponseAsync(
    "Explain quantum computing",
    systemPrompt: "You are a physics expert",
    onChunk: chunk => Console.Write(chunk)
);
```

#### 3. **ValidateApiKeyAsync** (API key validation)

- **Before**: Validated against `/v1/chat/completions`
- **After**: Validates against `/v1/responses` endpoint
- **Structure**: Now uses `input` field instead of `messages`

```csharp
// New usage
var (success, message) = await grokService.ValidateApiKeyAsync();
if (success)
    _logger.LogInformation("API key valid: {Message}", message);
```

### New Methods (Responses API Features)

#### 4. **GetResponseByIdAsync** - Retrieve stored responses

Retrieve a previously generated response by ID (available for 30 days).

```csharp
var responseText = await grokService.GetResponseByIdAsync("response-id-here");
```

**Endpoint**: `GET /v1/responses/{response_id}`

#### 5. **DeleteResponseAsync** - Delete stored responses

Delete a previously stored response (useful for cleanup and privacy).

```csharp
var deleted = await grokService.DeleteResponseAsync("response-id-here");
if (deleted)
    _logger.LogInformation("Response deleted successfully");
```

**Endpoint**: `DELETE /v1/responses/{response_id}`

#### 6. **ContinueConversationAsync** - Continue conversations

Continue a previous conversation without repeating context.

```csharp
// First interaction
var response1 = await grokService.GetSimpleResponse(
    "What is AI?",
    systemPrompt: "You are a tech expert"
);
// Extract responseId from response metadata

// Continue conversation
var response2 = await grokService.ContinueConversationAsync(
    "Tell me more about machine learning",
    previousResponseId: "first-response-id"
);
```

**Endpoint**: `POST /v1/responses` with `previous_response_id` parameter

#### 7. **ListAvailableModelsAsync** - List available models

Lists all models available through the xAI API.

```csharp
var models = await grokService.ListAvailableModelsAsync();
foreach (var model in models)
    Console.WriteLine($"Available model: {model}");
```

**Endpoint**: `GET /v1/models`

---

## Internal Changes

### Payload Builders

#### **CreateResponsesPayload** (NEW)

Builds request payloads for the `/v1/responses` endpoint with proper field names and structure.

```csharp
private Dictionary<string, object?> CreateResponsesPayload(
    string model,
    object[] input,
    bool stream,
    double? temperature = null)
{
    var payload = new Dictionary<string, object?>
    {
        ["model"] = model,
        ["input"] = input,              // Key change: "input" instead of "messages"
        ["stream"] = stream,
        ["store"] = true                // New: persist responses for 30 days
    };

    if (temperature.HasValue)
        payload["temperature"] = temperature.Value;

    // Penalties excluded for reasoning models
    if (!IsReasoningModel(model))
    {
        if (_defaultPresencePenalty.HasValue)
            payload["presence_penalty"] = _defaultPresencePenalty.Value;
        if (_defaultFrequencyPenalty.HasValue)
            payload["frequency_penalty"] = _defaultFrequencyPenalty.Value;
    }

    return payload;
}
```

#### **CreateChatRequestPayload** (LEGACY)

Retained for Semantic Kernel compatibility. Semantic Kernel's OpenAI connector still uses `/chat/completions` format. Direct HTTP methods use the new payload builder.

### Response ID Tracking

New internal dictionary for tracking response IDs across conversations:

```csharp
private readonly Dictionary<string, string> _conversationResponseIds = new();
```

This enables conversation continuation and cleanup operations.

---

## Semantic Kernel Integration Note

**Important**: The Semantic Kernel integration (`RunAgentAsync`, `RunAgentToChatBridgeAsync`) uses a dual approach:

1. **Semantic Kernel connector**: Still configured for `/chat/completions` endpoint (for backward compatibility with SK's OpenAI connector)
2. **Direct HTTP methods**: Use the new `/v1/responses` endpoint

This is handled by creating a separate `legacyEndpoint` URI for the SK connector:

```csharp
var legacyEndpoint = new Uri(_baseEndpoint!, "chat/completions");
builder.AddOpenAIChatCompletion(
    modelId: _model,
    apiKey: _apiKey,
    endpoint: legacyEndpoint,  // SK uses legacy endpoint
    serviceId: serviceId
);
```

**For new code**, prefer direct HTTP methods:

- `GetSimpleResponse()` - Non-streaming
- `GetStreamingResponseAsync()` - Streaming with SSE
- `ContinueConversationAsync()` - Conversation continuation
- `GetResponseByIdAsync()` - Response retrieval
- `DeleteResponseAsync()` - Response deletion

---

## Configuration

All existing configuration keys continue to work:

```json
{
  "Grok:ApiKey": "xai-...", // or XAI:ApiKey
  "Grok:Model": "grok-4", // or XAI:Model (default: grok-4)
  "Grok:Endpoint": "https://api.x.ai/v1", // or XAI:Endpoint
  "Grok:DefaultPresencePenalty": "0.0",
  "Grok:DefaultFrequencyPenalty": "0.0",
  "Grok:AutoSelectModelOnStartup": "false",
  "Grok:ValidateOnStartup": "true"
}
```

---

## Breaking Changes

### ⚠️ Response Parsing

Any code that manually parses responses from the old endpoint will break:

**Old (BROKEN):**

```csharp
// ❌ This no longer works with /responses endpoint
var choices = doc.RootElement.GetProperty("choices");
var content = choices[0].GetProperty("message").GetProperty("content");
```

**New (CORRECT):**

```csharp
// ✅ Use the refactored GetSimpleResponse or GetStreamingResponseAsync
var content = await grokService.GetSimpleResponse(userMessage);

// Or parse manually with new structure:
var output = doc.RootElement.GetProperty("output");
var content = output[0].GetProperty("content")[0].GetProperty("text");
```

### ✅ Non-Breaking

All public method signatures remain compatible:

- `GetSimpleResponse()` - same parameters and return type
- `GetStreamingResponseAsync()` - same parameters and return type
- `ValidateApiKeyAsync()` - same parameters and return type
- `RunAgentAsync()` - same parameters and return type
- `RunAgentToChatBridgeAsync()` - same parameters and return type

---

## Migration Checklist

### For Developers Using GrokAgentService

- [ ] Update any custom response parsing code (see Breaking Changes section)
- [ ] Consider using new `GetResponseByIdAsync()` and `DeleteResponseAsync()` for response management
- [ ] Test streaming responses with the new `/v1/responses` endpoint
- [ ] Verify API key validation still works
- [ ] Update unit tests to expect new response structure if applicable
- [ ] Review error handling (error messages from new endpoint may differ slightly)

### For API Consumers

- [ ] No changes needed if using high-level methods like `GetSimpleResponse()`
- [ ] Review if you need new conversation continuation feature via `ContinueConversationAsync()`
- [ ] Test response storage and retrieval if using `GetResponseByIdAsync()` and `DeleteResponseAsync()`

---

## Example: Complete Conversation Flow

```csharp
// 1. Initialize service
var grokService = new GrokAgentService(config, logger);
await grokService.InitializeAsync();

// 2. First query
var response1 = await grokService.GetSimpleResponse(
    "What is machine learning?",
    systemPrompt: "You are a computer science teacher"
);
Console.WriteLine("Assistant: " + response1);

// Response object contains an "id" field - in real implementation:
// var responseId = JsonDocument.Parse(response1)["id"].GetString();

// 3. Later: retrieve the stored response
var retrieved = await grokService.GetResponseByIdAsync("response-id-from-step-2");
Console.WriteLine("Retrieved: " + retrieved);

// 4. Continue conversation
var response2 = await grokService.ContinueConversationAsync(
    "Give me a concrete example",
    previousResponseId: "response-id-from-step-2"
);
Console.WriteLine("Assistant: " + response2);

// 5. Streaming with callbacks
await grokService.GetStreamingResponseAsync(
    "Explain neural networks",
    systemPrompt: "You are a ML expert",
    onChunk: chunk => Console.Write(chunk)
);

// 6. Cleanup
await grokService.DeleteResponseAsync("response-id-from-step-2");
```

---

## Testing

### Unit Test Updates

If your tests mock `GrokAgentService` responses, update test data:

**Old test data (BROKEN):**

```json
{
  "choices": [{ "message": { "content": "test response" } }]
}
```

**New test data (CORRECT):**

```json
{
  "output": [{ "content": [{ "text": "test response" }] }],
  "id": "test-response-id",
  "status": "completed"
}
```

### Integration Tests

Run the existing test suite to validate:

```bash
dotnet test tests/WileyWidget.WinForms.Tests -k Grok
```

---

## Error Handling

The service maintains existing error handling patterns:

- **No API key**: Returns diagnostic string "No API key configured for Grok"
- **Invalid model**: Attempts fallback to `grok-4` model
- **Rate limits (429)**: Exponential backoff retry with configurable max retries
- **Network errors**: Caught and logged with detailed messages
- **JSON parsing errors**: Logged and returns raw response body for debugging

---

## Performance Considerations

### Response Storage

The `/v1/responses` endpoint stores responses for **30 days**. This enables:

- ✅ Conversation continuation without full context re-transmission
- ✅ Response retrieval for audit/logging
- ✅ Reduced token usage for long conversations

### Streaming

Streaming behavior is identical to the old endpoint:

- ✅ Real-time chunks via Server-Sent Events (SSE)
- ✅ Callbacks invoked per chunk
- ✅ Full response assembled in memory

---

## Support and Troubleshooting

### Common Issues

1. **"Model X does not exist" error**
   - The service automatically falls back to `grok-4`
   - Check configured model in appsettings.json
   - Use `ListAvailableModelsAsync()` to see available models

2. **Response parsing errors**
   - Verify you're using `GetSimpleResponse()` or equivalent refactored methods
   - Check the actual response structure with `_logger.LogDebug()`
   - Use `GetResponseByIdAsync()` to retrieve and inspect stored responses

3. **Streaming stops abruptly**
   - Check cancellation token lifecycle
   - Verify network connectivity
   - Enable debug logging to see exact error

4. **Responses not storing**
   - Ensure `"store": true` is in the request (automatic in new methods)
   - Check API key permissions include response storage
   - Verify with `GetResponseByIdAsync()` within 30-day window

---

## References

- **X.ai Documentation**: <https://docs.x.ai/docs/api-reference#create-new-response>
- **Responses API Spec**: <https://docs.x.ai/docs/api-reference#create-new-response>
- **GrokAgentService Source**: `src/WileyWidget.WinForms/Services/AI/GrokAgentService.cs`

---

## Version History

| Date       | Version | Changes                                                                                     |
| ---------- | ------- | ------------------------------------------------------------------------------------------- |
| 2026-01-21 | 2.0.0   | Complete migration to /v1/responses endpoint, added response management, improved streaming |
| 2025-XX-XX | 1.0.0   | Original /v1/chat/completions implementation                                                |
