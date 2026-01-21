namespace WileyWidget.WinForms.Configuration;

/// <summary>
/// Carries command-line options that trigger the built-in report viewer.
/// </summary>
public record ReportViewerLaunchOptions(bool ShowReportViewer, string? ReportPath)
{
    public static readonly ReportViewerLaunchOptions Disabled = new(false, null);
}
