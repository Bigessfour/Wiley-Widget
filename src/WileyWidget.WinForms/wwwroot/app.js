// JARVIS Chat utility functions for Blazor WebView

window.scrollToBottom = function (element) {
  if (element && element) {
    element.scrollTop = element.scrollHeight;
  }
};

// Log startup
console.log("[JARVIS] JavaScript utilities loaded successfully");
