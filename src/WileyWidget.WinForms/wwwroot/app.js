window.scrollToBottom = function (element) {
  if (element) {
    element.scrollTop = element.scrollHeight;
  }
};

window.jarvisTheme = {
  mapThemeCss: function (themeName, isDark) {
    const normalized = (themeName || "").toLowerCase();
    if (
      isDark ||
      normalized.includes("dark") ||
      normalized.includes("black") ||
      normalized.includes("highcontrast")
    ) {
      return "fluent2-dark.css";
    }

    return "fluent2.css";
  },

  applyTheme: function (themeName, isDark) {
    const cssFile = window.jarvisTheme.mapThemeCss(themeName, isDark);
    const backgroundColor = isDark ? "#17181c" : "#f5f7fb";
    const foregroundColor = isDark ? "#f3f6fb" : "#17202b";
    let link = document.getElementById("syncfusion-blazor-theme");

    if (!link) {
      link = document.createElement("link");
      link.id = "syncfusion-blazor-theme";
      link.rel = "stylesheet";
      document.head.appendChild(link);
    }

    link.href = `_content/Syncfusion.Blazor.Themes/${cssFile}`;
    document.documentElement.setAttribute("data-jarvis-theme", themeName || "Office2019Colorful");
    document.documentElement.setAttribute("data-jarvis-mode", isDark ? "dark" : "light");
    document.documentElement.style.backgroundColor = backgroundColor;
    document.body.style.backgroundColor = backgroundColor;
    document.body.style.color = foregroundColor;

    const appHost = document.getElementById("app");
    if (appHost) {
      appHost.style.backgroundColor = backgroundColor;
      appHost.style.color = foregroundColor;
    }
  },
};

console.log("[JARVIS] JavaScript utilities loaded successfully");
