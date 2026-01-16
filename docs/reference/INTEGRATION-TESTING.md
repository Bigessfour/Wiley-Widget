# Integration Testing Guide - AI & Blazor

## JARVIS Blazor Integration
The `JARVISAssist.razor` component is hosted in a `BlazorWebView` within `JARVISChatHostForm`. Integration testing focuses on the bridge between JavaScript/Blazor and the C# services.

### Core Testing Areas
1. **Bridge Communication**:
   - Verify `SendMessageAsync` in Blazor triggers `IXAIService.GetStreamingResponse`.
   - Verify `OnMessageReceived` events correctly update the Blazor UI.

2. **Personality Adherence**:
   - Verify `JARVISPersonalityService` system prompt is the first message in the stream.

3. **Error Resilience**:
   - Handle webview initialization failures gracefully.

## ❌ Deprecated Tests
Tests for the following components have been REMOVED as the code is no longer in the project:
- `ChatPanelTests.cs`
- `AIChatControl` UI tests
- `ChatPanelViewModel` unit tests
