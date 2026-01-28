# Blazor Integration Architecture

**Status**: Active on "polish" branch | **Last Updated**: 2026-01-25

## Overview

Wiley-Widget integrates **Blazor WebView** to host the **JARVIS Assistant** chat component within the Windows Forms application. This document explains the integration pattern, dependency injection, lifecycle, and troubleshooting.

## Architecture Diagram

```
┌─────────────────────────────────────┐
│      MainForm (WinForms)            │
│  ┌───────────────────────────────┐  │
│  │   RightPanel (WebView Host)   │  │
│  │  ┌─────────────────────────┐  │  │
│  │  │ WindowsFormsBlazorWebView│  │  │
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │  JARVISAssist.razor  │  │  │
│  │  │  │  (Chat Component) │  │  │  │
│  │  │  └───────────────────┘  │  │  │
│  │  │                         │  │  │
│  │  │  Services (DI):         │  │  │
│  │  │  • IUserContext         │  │  │
│  │  │  • IChatBridgeService   │  │  │
│  │  │  • IJSRuntime           │  │  │
│  │  │  • ILogger              │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
│                                     │
│   WinForms ↔ Blazor Bridge:         │
│   • IChatBridgeService              │
│   • IUserContext (scoped lifetime)  │
└─────────────────────────────────────┘
```

---

## Core Components

### **1. RightPanel (WebView Host)**

**File**: `src/WileyWidget.WinForms/Forms/RightPanel.cs`

The `RightPanel` is a Windows Forms `Form` that hosts a `WindowsFormsBlazorWebView` control.

```csharp
public class RightPanel : Form
{
    private readonly WindowsFormsBlazorWebView _blazorWebView;
    private readonly ILogger<RightPanel> _logger;

    public RightPanel(IServiceProvider parentServiceProvider,
                      ILogger<RightPanel> logger)
    {
        _logger = logger;

        // 1. Create WebView control
        _blazorWebView = new WindowsFormsBlazorWebView
        {
            Dock = DockStyle.Fill,
            StartPath = "/"
        };

        // 2. Configure Blazor DI container (WebView-scoped)
        ConfigureBlazorServices(_blazorWebView.Services, parentServiceProvider);

        // 3. Register root Blazor component
        _blazorWebView.RootComponents.Add(new RootComponent
        {
            ComponentType = typeof(JARVISAssist),
            Selector = "#app",
            Parameters = new Dictionary<string, object?>()
        });

        // 4. Add to form
        Controls.Add(_blazorWebView);

        _logger.LogInformation("RightPanel initialized with Blazor WebView");
    }

    private void ConfigureBlazorServices(IServiceCollection services,
                                         IServiceProvider parent)
    {
        // Windows Forms Blazor core
        services.AddWindowsFormsBlazorWebView();

        // Syncfusion Blazor components
        services.AddSyncfusionBlazor();

        // AI and chat services
        services.AddScoped<IChatBridgeService>(sp =>
            parent.GetRequiredService<IChatBridgeService>());

        // User context (scoped per WebView lifetime)
        services.AddScoped<IUserContext>(sp =>
            parent.GetRequiredService<IUserContext>());

        // Logging
        services.AddLogging(builder =>
            builder.AddDebug());
    }
}
```

**Key Points:**

- ✅ `Dock = DockStyle.Fill` ensures WebView fills parent panel
- ✅ Services are scoped to WebView lifetime (NOT singleton)
- ✅ `IUserContext` comes from parent DI container but lives within WebView scope
- ✅ `IChatBridgeService` bridges WinForms ↔ Blazor communication

### **2. JARVISAssist Blazor Component**

**File**: `src/WileyWidget.WinForms/UI/Components/JARVISAssist.razor`

The `JARVISAssist` component is a Blazor component hosted in RightPanel's WebView.

```razor
@page "/"
@using WileyWidget.Services.Abstractions
@using Microsoft.Extensions.Logging
@inject IUserContext UserContext
@inject IChatBridgeService ChatBridge
@inject IJSRuntime JS
@inject ILogger<JARVISAssist> Logger

<div class="jarvis-container">
    <div class="chat-header">
        <h3>JARVIS Assistant</h3>
        <small>@UserContext?.CurrentUser?.UserName</small>
    </div>

    <div class="chat-messages" @ref="messagesDiv">
        @foreach (var message in messages)
        {
            <div class="message @(message.IsUserMessage ? "user" : "bot")">
                <div class="message-text">@((MarkupString)message.HtmlContent)</div>
                <div class="message-time">@message.Timestamp.ToString("HH:mm:ss")</div>
            </div>

            @if (message.Id == loadingMessageId && isLoading)
            {
                <div class="typing-indicator">
                    <span></span><span></span><span></span>
                </div>
            }
        }
    </div>

    <div class="chat-input">
        <textarea @bind="userInput"
                  @onkeydown="HandleKeyDown"
                  placeholder="Ask JARVIS..."
                  disabled="@isLoading" />
        <button @onclick="SendMessage" disabled="@isLoading">
            @if (isLoading)
            {
                <span class="spinner"></span>
            }
            else
            {
                <span>Send</span>
            }
        </button>
    </div>
</div>

@code {
    private List<ChatMessage> messages = new();
    private string userInput = "";
    private bool isLoading = false;
    private string? loadingMessageId;
    private ElementReference messagesDiv;

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("JARVISAssist initializing for user {UserName}",
            UserContext.CurrentUser?.UserName);

        // Subscribe to chat bridge events
        ChatBridge.OnMessageReceived += HandleRemoteMessage;

        // Load chat history
        messages = await ChatBridge.GetChatHistoryAsync(UserContext.CurrentUserId);
        await InvokeAsync(StateHasChanged);
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return;

        // Add user message
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Text = userInput,
            IsUserMessage = true,
            Timestamp = DateTime.Now,
            UserId = UserContext.CurrentUserId
        };
        messages.Add(userMsg);

        // Show loading state
        loadingMessageId = Guid.NewGuid().ToString();
        isLoading = true;
        userInput = "";
        await ScrollToBottom();
        await InvokeAsync(StateHasChanged);

        try
        {
            // Send to JARVIS (xAI Grok API via ChatBridgeService)
            var response = await ChatBridge.SendMessageAsync(
                userMsg.Text,
                UserContext.CurrentUserId,
                CancellationToken.None
            );

            // Add bot response
            var botMsg = new ChatMessage
            {
                Id = loadingMessageId,
                Text = response.Text,
                HtmlContent = response.HtmlContent,  // Markdown rendered
                IsUserMessage = false,
                Timestamp = DateTime.Now
            };
            messages[messages.Count - 1] = botMsg;  // Replace loading placeholder
            await ScrollToBottom();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending message to JARVIS");
            messages.Add(new ChatMessage
            {
                Text = $"Error: {ex.Message}",
                IsUserMessage = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleRemoteMessage(ChatMessage message)
    {
        Logger.LogInformation("Remote message received: {MessageId}", message.Id);
        messages.Add(message);
        await ScrollToBottom();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ScrollToBottom()
    {
        await JS.InvokeVoidAsync("scrollToBottom", messagesDiv);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Code == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }

    public void Dispose()
    {
        ChatBridge.OnMessageReceived -= HandleRemoteMessage;
    }
}
```

**CSS Styling**:

```css
.jarvis-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto;
}

.chat-header {
  padding: 12px;
  border-bottom: 1px solid #e0e0e0;
  background: #f5f5f5;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

.message {
  margin: 8px 0;
  padding: 8px 12px;
  border-radius: 4px;
  max-width: 70%;
}

.message.user {
  background: #e3f2fd;
  margin-left: auto;
}

.message.bot {
  background: #f5f5f5;
}

.typing-indicator {
  display: flex;
  gap: 4px;
  margin: 12px 0;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #666;
  animation: bounce 1.4s infinite;
}

.typing-indicator span:nth-child(2) {
  animation-delay: 0.2s;
}
.typing-indicator span:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes bounce {
  0%,
  80%,
  100% {
    opacity: 0.4;
    transform: translateY(0);
  }
  40% {
    opacity: 1;
    transform: translateY(-8px);
  }
}

.chat-input {
  display: flex;
  gap: 8px;
  padding: 12px;
  border-top: 1px solid #e0e0e0;
}

textarea {
  flex: 1;
  resize: none;
  min-height: 40px;
  max-height: 120px;
  padding: 8px;
  border: 1px solid #ccc;
  border-radius: 4px;
  font-family: inherit;
}

button {
  padding: 8px 16px;
  background: #1976d2;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
```

---

## Dependency Injection

### **Service Registration (Program.cs)**

The DI container must register all services used by Blazor components:

```csharp
public static void ConfigureServices(IServiceCollection services)
{
    // Core WinForms services
    services.AddSingleton<IThemeService, ThemeService>();
    services.AddSingleton<IPanelNavigationService, PanelNavigationService>();

    // Chat/Blazor services
    services.AddScoped<IUserContext, UserContext>();
    services.AddScoped<IChatBridgeService, ChatBridgeService>();

    // Logging
    services.AddLogging(builder =>
        builder.AddDebug().AddConsole());

    // AI integration
    services.AddHttpClient<IxAIGrokClient, xAIGrokClient>()
        .ConfigureHttpClient(client =>
        {
            client.BaseAddress = new Uri("https://api.x.ai/v1");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        });
}
```

### **Service Lifetimes**

| Service              | Lifetime  | Scope    | Notes                       |
| -------------------- | --------- | -------- | --------------------------- |
| `IThemeService`      | Singleton | App-wide | Shared theme state          |
| `IUserContext`       | Scoped    | WebView  | Per-session user data       |
| `IChatBridgeService` | Scoped    | WebView  | Per-chat-session bridging   |
| `IxAIGrokClient`     | Scoped    | WebView  | HTTP client for AI API      |
| `ILogger<T>`         | Scoped    | WebView  | Debug logging per component |

### **CRITICAL: IUserContext Scope**

`IUserContext` is **scoped to WebView lifetime**, not global singleton:

```csharp
// ✅ CORRECT: Scoped in WebView services
services.AddScoped<IUserContext, UserContext>();

// ❌ WRONG: Singleton would share user data across instances
services.AddSingleton<IUserContext, UserContext>();
```

**Why Scoped?**

- Each RightPanel WebView instance is independent
- User context changes per session should not affect other instances
- Proper cleanup when WebView is disposed

---

## WebView2 Runtime Dependency

`WindowsFormsBlazorWebView` requires **WebView2 Runtime** to be installed.

### **Installation**

1. **End-User**: Download from [Microsoft Edge WebView2 Downloads](https://developer.microsoft.com/microsoft-edge/webview2/)
2. **Developers**: Install via NuGet (bundled in `Microsoft.AspNetCore.Components.WebView.WindowsForms`)

### **Project Configuration**

The `.csproj` file should have:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebView.WindowsForms"
                      Version="9.0.0" />
    <PackageReference Include="Syncfusion.Blazor"
                      Version="25.2.2" />
</ItemGroup>

<!-- WebView2 runtime binding redirect -->
<ItemGroup>
    <RuntimeHostConfigurationOption
        Include="Switch.System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"
        Value="false" />
</ItemGroup>
```

### **Troubleshooting WebView2**

| Issue                            | Cause                              | Fix                              |
| -------------------------------- | ---------------------------------- | -------------------------------- |
| "WebView2 Runtime not found"     | Runtime not installed              | Install from Microsoft downloads |
| "WebView initialization timeout" | Runtime corrupted or network issue | Reinstall WebView2               |
| "Blazor component not loading"   | HTML/JS path incorrect             | Verify `StartPath = "/"`         |

---

## IChatBridgeService (WinForms ↔ Blazor Bridge)

**File**: `src/WileyWidget.Services/ChatBridgeService.cs`

The `IChatBridgeService` enables two-way communication between WinForms and Blazor.

```csharp
public interface IChatBridgeService
{
    event EventHandler<ChatMessage> OnMessageReceived;

    Task<ChatResponse> SendMessageAsync(
        string message,
        string userId,
        CancellationToken ct);

    Task<List<ChatMessage>> GetChatHistoryAsync(string userId);

    Task ClearChatHistoryAsync(string userId);
}

public class ChatBridgeService : IChatBridgeService
{
    private readonly IxAIGrokClient _grokClient;
    private readonly ILogger<ChatBridgeService> _logger;

    public event EventHandler<ChatMessage>? OnMessageReceived;

    public ChatBridgeService(IxAIGrokClient grokClient,
                             ILogger<ChatBridgeService> logger)
    {
        _grokClient = grokClient;
        _logger = logger;
    }

    public async Task<ChatResponse> SendMessageAsync(
        string message,
        string userId,
        CancellationToken ct)
    {
        _logger.LogInformation("Sending message to JARVIS: {Message}", message);

        var response = await _grokClient.CompleteAsync(
            new CompletionRequest
            {
                Messages = new[]
                {
                    new Message { Role = "user", Content = message }
                },
                Model = "grok-3-latest"
            },
            ct
        );

        var chatResponse = new ChatResponse
        {
            Text = response.Choices[0].Message.Content,
            HtmlContent = MarkdownToHtml(response.Choices[0].Message.Content),
            Timestamp = DateTime.Now
        };

        OnMessageReceived?.Invoke(this, new ChatMessage
        {
            Text = chatResponse.Text,
            IsUserMessage = false,
            Timestamp = chatResponse.Timestamp,
            UserId = userId
        });

        return chatResponse;
    }

    private string MarkdownToHtml(string markdown)
    {
        // Use Markdig or similar to convert Markdown → HTML
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return Markdown.ToHtml(markdown, pipeline);
    }
}
```

### **Message Flow**

```
User Types Message in JARVISAssist
         ↓
JARVISAssist.SendMessage()
         ↓
ChatBridgeService.SendMessageAsync()
         ↓
xAIGrokClient.CompleteAsync() [HTTP → xAI API]
         ↓
Response received
         ↓
OnMessageReceived event fires
         ↓
JARVISAssist.HandleRemoteMessage() [Blazor event handler]
         ↓
Message added to messages list
         ↓
StateHasChanged() → UI re-renders
```

---

## Component Lifecycle

### **Initialization Sequence**

1. **App Startup** (`Program.Main`)
   - Configure DI container
   - Call `InitializeTheme()`

2. **MainForm Construction**
   - Apply theme via `ThemeColors.ApplyTheme()`
   - Create RightPanel with DI container

3. **RightPanel Construction**
   - Create `WindowsFormsBlazorWebView`
   - Configure Blazor services (WebView-scoped DI)
   - Register `JARVISAssist` root component
   - Add to form controls

4. **JARVISAssist Initialization**
   - `OnInitializedAsync()` called
   - Subscribe to `ChatBridge.OnMessageReceived`
   - Load chat history
   - Render component

5. **User Interaction**
   - User types message
   - `SendMessage()` invoked
   - Message sent to xAI Grok API
   - Response displayed with typing animation

### **Cleanup (App Shutdown)**

```csharp
public override void Dispose(bool disposing)
{
    if (disposing)
    {
        ChatBridge.OnMessageReceived -= HandleRemoteMessage;
        _blazorWebView?.Dispose();
    }
    base.Dispose(disposing);
}
```

---

## Syncfusion Blazor Components

Wiley-Widget uses Syncfusion Blazor components within JARVISAssist (future enhancements):

```razor
@using Syncfusion.Blazor
@using Syncfusion.Blazor.Inputs
@using Syncfusion.Blazor.Buttons

<SfButton OnClick="@SendMessage">Send</SfButton>
<SfTextBox @bind-Value="userInput" />
```

**Available Components:**

- `SfTextBox`: Rich text input with formatting
- `SfButton`: Themed buttons
- `SfMenuBar`: Command menu
- `SfTooltip`: Tooltips for UI hints
- `SfSpinner`: Loading indicator

**Theme Integration:**
Syncfusion Blazor components **automatically inherit** the WinForms theme set via `SfSkinManager.ApplicationVisualTheme`.

---

## Testing Blazor Components

### **Unit Tests**

Located in: `tests/WileyWidget.WinForms.Tests/Unit/Forms/RightPanelTests.cs`

```csharp
public class RightPanelTests
{
    [Fact]
    public void RightPanel_InitializesBlazeWebView_WithDIServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IChatBridgeService, ChatBridgeService>();
        var provider = services.BuildServiceProvider();

        // Act
        var rightPanel = new RightPanel(provider, Mock.Of<ILogger<RightPanel>>());

        // Assert
        rightPanel.Controls.Count.Should().BeGreaterThan(0);
        rightPanel.Controls[0].Should().BeOfType<WindowsFormsBlazorWebView>();
    }

    [Fact]
    public void RightPanel_RegistersBlazorServices_Correctly()
    {
        // Arrange
        var services = new ServiceCollection();
        var parent = services.BuildServiceProvider();

        // Act
        var rightPanel = new RightPanel(parent, Mock.Of<ILogger<RightPanel>>());

        // Assert: Blazor WebView should have DI configured
        // (Implementation depends on Blazor testing framework)
    }
}
```

### **UI/E2E Tests**

For full integration testing of chat functionality, use Playwright or Selenium:

```csharp
[TestFixture]
public class JARVISAssistE2ETests
{
    private IPage _page;

    [SetUp]
    public async Task Setup()
    {
        var browser = await Playwright.Chromium.LaunchAsync();
        _page = await browser.NewPageAsync();
        await _page.GotoAsync("http://localhost:5000");  // Local WebView URL
    }

    [Test]
    public async Task SendMessage_DisplaysTypingIndicator_ThenResponse()
    {
        // Arrange
        var messageInput = _page.Locator("textarea");
        var sendButton = _page.Locator("button:has-text('Send')");

        // Act
        await messageInput.FillAsync("What is the budget forecast?");
        await sendButton.ClickAsync();

        // Assert
        await expect(_page.Locator(".typing-indicator")).ToBeVisibleAsync();
        await expect(_page.Locator(".message.bot")).ToContainTextAsync("forecast", new() { Timeout = 10000 });
    }
}
```

---

## Performance Considerations

### **Chat History Loading**

For large chat histories (100+ messages), optimize loading:

```csharp
// ❌ SLOW: Load all messages at once
var messages = await ChatBridge.GetChatHistoryAsync(userId);

// ✅ FAST: Paginate messages
var page1 = await ChatBridge.GetChatHistoryAsync(userId, pageSize: 20, page: 1);
// Load older messages on scroll
```

### **Message Rendering**

For smooth scrolling with many messages:

```razor
@foreach (var message in messages.TakeLast(50))  // ✅ Render last 50 only
{
    <div class="message" @key="message.Id">
        @((MarkupString)message.HtmlContent)
    </div>
}
```

### **API Call Debouncing**

Prevent rapid successive API calls:

```csharp
private DateTime _lastMessageSentAt = DateTime.MinValue;
private const int MinMessageIntervalMs = 500;

private async Task SendMessage()
{
    var now = DateTime.Now;
    if ((now - _lastMessageSentAt).TotalMilliseconds < MinMessageIntervalMs)
        return;

    _lastMessageSentAt = now;
    // ... send message
}
```

---

## Troubleshooting

### **"IUserContext is null"**

**Symptom**: `NullReferenceException` in JARVISAssist when accessing `UserContext`

**Cause**: Service not registered in WebView DI container

**Fix**:

```csharp
private void ConfigureBlazorServices(IServiceCollection services,
                                     IServiceProvider parent)
{
    // ✅ Register IUserContext
    services.AddScoped<IUserContext>(sp =>
        parent.GetRequiredService<IUserContext>());
}
```

### **"Blazor component not rendering"**

**Symptom**: RightPanel shows blank white area

**Cause**: WebView2 runtime missing or `StartPath` incorrect

**Fix**:

```csharp
_blazorWebView.StartPath = "/";  // Ensure correct path
// Install WebView2 runtime from Microsoft
```

### **"Chat messages not updating"**

**Symptom**: Message sent but response not displayed

**Cause**: `StateHasChanged()` not called or event not firing

**Fix**:

```csharp
private async Task HandleRemoteMessage(ChatMessage message)
{
    messages.Add(message);
    await InvokeAsync(StateHasChanged);  // ✅ Invoke on UI thread
}
```

### **"Memory leak: WebView not disposed"**

**Symptom**: High memory usage after closing RightPanel multiple times

**Cause**: Blazor WebView not properly disposed

**Fix**:

### **"Memory leak: WebView not disposed"**

**Symptom**: High memory usage after closing RightPanel multiple times

**Cause**: Blazor WebView not properly disposed

**Fix**:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        ChatBridge?.OnMessageReceived -= HandleRemoteMessage;
        _blazorWebView?.Dispose();  // ✅ Explicit disposal
    }
    base.Dispose(disposing);
}
```

---

## References

- **Windows Forms Blazor**: <https://learn.microsoft.com/aspnet/core/blazor/hybrid/tutorials/windows-forms>
- **Blazor Lifecycle**: <https://learn.microsoft.com/aspnet/core/blazor/components/lifecycle>
- **WebView2 Runtime**: <https://developer.microsoft.com/microsoft-edge/webview2/>
- **Syncfusion Blazor**: <https://www.syncfusion.com/blazor-components>
- **xAI Grok API**: <https://docs.x.ai/api/introduction>
- **UI Components**: [UI_COMPONENTS.md](UI_COMPONENTS.md)
- **Chat Bridge Service**: `src/WileyWidget.Services/ChatBridgeService.cs`
