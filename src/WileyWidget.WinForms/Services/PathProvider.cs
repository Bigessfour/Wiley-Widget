using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using WileyWidget.WinForms.Configuration;
using Serilog;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Default implementation of <see cref="IPathProvider"/>.
    /// Respects UI test-harness mode and app configuration (Paths:ExportDirectory).
    /// Production default: %LocalAppData%/WileyWidget/Exports
    /// Test default: Path.GetTempPath()
    /// </summary>
    public sealed class PathProvider : IPathProvider
    {
        private readonly IConfiguration _configuration;
        private readonly UIConfiguration _uiConfig;

        public PathProvider(IConfiguration configuration, UIConfiguration uiConfig)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _uiConfig = uiConfig ?? throw new ArgumentNullException(nameof(uiConfig));
        }

        public string GetExportDirectory()
        {
            try
            {
                // Detect test harness via UIConfiguration *or* environment variable for CI/test runners
                var envVal = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS") ?? string.Empty;
                var envTest = string.Equals(envVal, "1", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(envVal, "true", StringComparison.OrdinalIgnoreCase);

                if (_uiConfig.IsUiTestHarness || envTest)
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "wileytests");
                    EnsureDirectory(tmp);
                    Log.Debug("Test harness active - using temp path for exports: {Path}", tmp);
                    return tmp;
                }

                // Respect explicit configuration when provided
                var configured = _configuration["Paths:ExportDirectory"];
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    EnsureDirectory(configured);
                    return configured!;
                }

                // Default location: LocalApplicationData/WileyWidget/Exports
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(localAppData)) localAppData = Path.GetTempPath();

                var dir = Path.Combine(localAppData, "WileyWidget", "Exports");
                EnsureDirectory(dir);
                return dir;
            }
            catch (Exception ex)
            {
                // Fallback to temp folder - do not fail application startup just for export path
                Log.Warning(ex, "Failed to determine export directory - falling back to temp path");
                return Path.GetTempPath();
            }
        }

        private static void EnsureDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create directory {Path}", path);
            }
        }
    }
}
