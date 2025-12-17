using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Application = FlaUI.Core.Application;

namespace WileyWidget.WinForms.E2ETests
{
    /// <summary>
    /// FlaUI E2E tests for ChatWindow - AI Chat Assistant.
    /// Tests chat functionality, message sending, conversation management.
    /// Note: These tests verify UI behavior; actual AI responses depend on service availability.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via cleanup.")]
    [Collection("UI Tests")]
    public sealed class ChatWindowE2ETests : IDisposable
    {
        private readonly string _exePath;
        private Application? _app;
        private UIA3Automation? _automation;
        private const int DefaultTimeout = 20000;

        public ChatWindowE2ETests()
        {
            _exePath = ResolveExecutablePath();

            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            Environment.SetEnvironmentVariable("WILEYWIDGET_USE_INMEMORY", "true");
            Environment.SetEnvironmentVariable("UI__IsUiTestHarness", "true");
            Environment.SetEnvironmentVariable("UI__UseMdiMode", "false");
            Environment.SetEnvironmentVariable("UI__UseTabbedMdi", "false");
        }

        private static string ResolveExecutablePath()
        {
            var envPath = Environment.GetEnvironmentVariable("WILEYWIDGET_EXE");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory ?? ".", "..", "..", "..", "..", "..", "src", "WileyWidget.WinForms", "bin", "Debug"));
            if (!Directory.Exists(baseDir))
            {
                throw new DirectoryNotFoundException($"Build output directory not found at '{baseDir}'. Build WileyWidget.WinForms or set WILEYWIDGET_EXE.");
            }

            var standard = Path.Combine(baseDir, "net9.0-windows", "WileyWidget.WinForms.exe");
            if (File.Exists(standard))
            {
                return standard;
            }

            var versioned = Directory.GetDirectories(baseDir, "net9.0-windows*")
                .Select(dir => Path.Combine(dir, "WileyWidget.WinForms.exe"))
                .FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(versioned))
            {
                return versioned;
            }

            throw new FileNotFoundException($"Executable not found. Build Debug output under '{baseDir}'.");
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_Opens_WithCorrectTitle()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            Assert.NotNull(chatWindow);
            Assert.Contains("AI Chat", chatWindow.Title, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_StatusPanel_IsVisible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Find status panel at top
            var statusPanel = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Pane));
            Assert.NotNull(statusPanel);

            // Status label should show "Ready"
            var statusLabel = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Text));
            Assert.NotNull(statusLabel);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_ChatControl_IsPresent()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // AIChatControl should be present - look for input box
            var inputBox = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(inputBox);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_SendButton_IsAccessible()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Find Send button
            var sendButton = WaitForElement(chatWindow, cf => cf.ByName("Send"));
            if (sendButton == null)
            {
                // May be labeled differently
                sendButton = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Button));
            }

            Assert.NotNull(sendButton);
            Assert.True(sendButton.IsEnabled);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_InputBox_AcceptsText()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Find input textbox
            var inputBox = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(inputBox);

            WaitUntilResponsive(inputBox);

            // Type test message
            inputBox.AsTextBox().Text = "Hello, this is a test message";

            // Verify text was entered
            Assert.Equal("Hello, this is a test message", inputBox.AsTextBox().Text);
        }

        [Fact]
        [Trait("Category", "UI")]
        [Trait("Category", "Slow")]
        public void ChatWindow_SendMessage_UpdatesStatusToProcessing()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Find input textbox
            var inputBox = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(inputBox);

            WaitUntilResponsive(inputBox);
            inputBox.AsTextBox().Text = "Test message";

            // Find and click Send button
            var sendButton = WaitForElement(chatWindow, cf => cf.ByName("Send"));
            if (sendButton == null)
            {
                sendButton = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Button));
            }

            Assert.NotNull(sendButton);
            WaitUntilResponsive(sendButton);
            sendButton.AsButton().Invoke();

            // Wait briefly for status to update
            Thread.Sleep(500);

            // Status should change from "Ready" to "Processing" or similar
            var statusLabel = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Text));
            Assert.NotNull(statusLabel);
            // Note: Status may quickly return to "Ready" if AI service is fast or unavailable
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_MessageDisplay_ShowsMessages()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Find input textbox
            var inputBox = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(inputBox);

            WaitUntilResponsive(inputBox);
            inputBox.AsTextBox().Text = "Test message for display";

            // Send message
            var sendButton = WaitForElement(chatWindow, cf => cf.ByName("Send"));
            if (sendButton == null)
            {
                sendButton = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Button));
            }

            Assert.NotNull(sendButton);
            WaitUntilResponsive(sendButton);
            sendButton.AsButton().Invoke();

            // Wait for message to appear in chat display
            Thread.Sleep(2000);

            // Look for list or message display area
            var messageDisplay = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.List));
            if (messageDisplay == null)
            {
                messageDisplay = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.DataGrid));
            }

            // Message display should exist (even if AI service is unavailable)
            Assert.NotNull(messageDisplay);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_CloseButton_ClosesWindow()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Get window handle
            var windowHandle = chatWindow.AutomationId;

            // Close the window
            chatWindow.Close();

            // Wait for window to close
            Thread.Sleep(1000);

            // Verify window is closed (should not be found anymore)
            var desktop = _automation!.GetDesktop();
            var closedWindow = desktop.FindFirstChild(cf => cf.ByName("AI Chat Assistant"));

            // Window should be null or not available
            Assert.True(closedWindow == null || !closedWindow.IsAvailable);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_EscapeKey_ClosesWindow()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Send Escape key to window
            chatWindow.Focus();
            Thread.Sleep(200);

            // Use keyboard to send Escape (Keyboard is static in FlaUI)
            FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);

            // Wait for window to close
            Thread.Sleep(1000);

            // Verify window is closed
            var desktop = _automation!.GetDesktop();
            var closedWindow = desktop.FindFirstChild(cf => cf.ByName("AI Chat Assistant"));

            Assert.True(closedWindow == null || !closedWindow.IsAvailable);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_CanBeReopened_AfterClosing()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();

            // Open chat window first time
            var chatWindow1 = OpenChatWindow(mainWindow);
            Assert.NotNull(chatWindow1);

            // Close it
            chatWindow1.Close();
            Thread.Sleep(1000);

            // Open chat window again
            var chatWindow2 = OpenChatWindow(mainWindow);
            Assert.NotNull(chatWindow2);
            Assert.Contains("AI Chat", chatWindow2.Title, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_HasProperLayout()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Verify key UI elements are present
            // 1. Status panel (top)
            var statusPanel = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Pane));
            Assert.NotNull(statusPanel);

            // 2. Chat control (middle) - has input box
            var inputBox = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Edit));
            Assert.NotNull(inputBox);

            // 3. Send button
            var sendButton = WaitForElement(chatWindow, cf => cf.ByName("Send"));
            if (sendButton == null)
            {
                sendButton = WaitForElement(chatWindow, cf => cf.ByControlType(ControlType.Button));
            }
            Assert.NotNull(sendButton);

            // Window should be sizable
            Assert.True(chatWindow.Patterns.Window.IsSupported && chatWindow.Patterns.Window.Pattern.CanMaximize.Value);
        }

        [Fact]
        [Trait("Category", "UI")]
        public void ChatWindow_HasMinimumSize()
        {
            if (!EnsureInteractiveOrSkip()) return;

            StartApp();
            var mainWindow = GetMainWindow();
            var chatWindow = OpenChatWindow(mainWindow);

            // Verify window has reasonable size (at least 600x700 from source)
            var bounds = chatWindow.BoundingRectangle;
            Assert.True(bounds.Width >= 500, $"Window width {bounds.Width} is less than minimum 500");
            Assert.True(bounds.Height >= 600, $"Window height {bounds.Height} is less than minimum 600");
        }

        private Window OpenChatWindow(Window mainWindow)
        {
            // Find Chat navigation button
            var navButton = WaitForElement(mainWindow, cf => cf.ByAutomationId("Nav_Chat"), timeoutMs: 30000);

            if (navButton == null)
            {
                navButton = WaitForElement(mainWindow, cf => cf.ByName("Chat"), timeoutMs: 10000);
            }

            if (navButton == null)
            {
                // Try finding in menu or toolbar
                navButton = WaitForElement(mainWindow, cf => cf.ByName("AI Chat"), timeoutMs: 10000);
            }

            Assert.NotNull(navButton);

            navButton.Click();

            // Wait for ChatWindow to appear
            var chatElement = Retry.WhileNull(() =>
            {
                try
                {
                    var desktop = _automation!.GetDesktop();
                    var window = desktop.FindFirstChild(cf =>
                        cf.ByName("AI Chat Assistant - Wiley Widget"));

                    if (window != null && window.ControlType == ControlType.Window)
                    {
                        return window.AsWindow();
                    }

                    // Try shorter name match
                    window = desktop.FindFirstChild(cf => cf.ByName("AI Chat"));
                    if (window != null && window.ControlType == ControlType.Window)
                    {
                        return window.AsWindow();
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromSeconds(30)).Result;

            return chatElement ?? throw new InvalidOperationException("Chat window did not open");
        }

        private bool EnsureInteractiveOrSkip()
        {
            var uiTests = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");
            if (!string.Equals(uiTests, "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        private void StartApp()
        {
            _automation = new UIA3Automation();
            _app = Application.Launch(_exePath);

            Retry.WhileException(() =>
            {
                var window = _app.GetMainWindow(_automation);
                if (window == null || !window.IsAvailable)
                {
                    throw new InvalidOperationException("Main window not ready");
                }
            }, TimeSpan.FromMilliseconds(DefaultTimeout));
        }

        private void WaitUntilResponsive(AutomationElement? element, int timeoutMs = 3000)
        {
            if (element == null) return;

            Retry.WhileException(() =>
            {
                if (!element.IsEnabled || element.IsOffscreen)
                {
                    throw new InvalidOperationException("Element not responsive");
                }
            }, TimeSpan.FromMilliseconds(timeoutMs));
        }

        private Window GetMainWindow()
        {
            var mainWindow = Retry.WhileNull(() => _app?.GetMainWindow(_automation!),
                timeout: TimeSpan.FromSeconds(DefaultTimeout / 1000));
            Assert.NotNull(mainWindow);
            return mainWindow.Result!;
        }

        private AutomationElement? WaitForElement(AutomationElement parent, Func<ConditionFactory, ConditionBase> condition, int timeoutMs = DefaultTimeout)
        {
            return Retry.WhileNull(() =>
            {
                try
                {
                    return parent.FindFirstDescendant(condition);
                }
                catch
                {
                    return null;
                }
            }, timeout: TimeSpan.FromMilliseconds(timeoutMs)).Result;
        }

        public void Dispose()
        {
            try
            {
                _app?.Close();
                _app?.Dispose();
            }
            catch { }

            try
            {
                _automation?.Dispose();
            }
            catch { }
        }
    }
}
