namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Provides application-scoped filesystem paths (export directory, etc.) in a testable way.
    /// Implementations should avoid using interactive Desktop paths when running in test mode.
    /// </summary>
    public interface IPathProvider
    {
        /// <summary>
        /// Returns a directory path suitable for writing exported files (CSV, reports, etc.).
        /// Caller may assume the directory exists or may create it as needed.
        /// </summary>
        string GetExportDirectory();
    }
}
