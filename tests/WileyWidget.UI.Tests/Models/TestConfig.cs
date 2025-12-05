namespace WileyWidget.UI.Tests.Models
{
    /// <summary>
    /// Shared config for FlaUI tests (guideline: records for data classes).
    /// </summary>
    public record TestConfig(string AppPath, TimeSpan DefaultTimeout, bool DarkMode = false);
}
