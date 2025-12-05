using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WileyWidget.UI.Tests.Infrastructure;

namespace WileyWidget.UI.Tests
{
    public class UITestFixture : ICollectionFixture<FlaUITestBase>, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }
        public IOptions<TestConfig> ConfigOptions { get; }

        public UITestFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Config: Path from env/build, DarkMode for themes
            var appPath = Environment.GetEnvironmentVariable("WILEY_UI_TEST_APP_PATH") ?? GetBuiltAppPath();
            services.Configure<TestConfig>(c =>
            {
                c.AppPath = appPath;
                c.DefaultTimeout = TimeSpan.FromSeconds(45); // Bump for reliability
                c.DarkMode = true; // Fluent Dark default
            });

            ServiceProvider = services.BuildServiceProvider();
            ConfigOptions = ServiceProvider.GetRequiredService<IOptions<TestConfig>>();

            // Ensure app built
            EnsureAppBuilt();
        }

        private string GetBuiltAppPath()
        {
            // LINQ search for exe (guideline-optimized)
            var solutionDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
            var possiblePaths = new[]
            {
                Path.Combine(solutionDir, "WileyWidget.WinForms", "bin", "Debug", "net9.0-windows", "WileyWidget.WinForms.exe"),
                Path.Combine(solutionDir, "WileyWidget.WinForms", "bin", "Release", "net9.0-windows", "WileyWidget.WinForms.exe")
            }.Where(File.Exists).ToArray();

            return possiblePaths.FirstOrDefault() ?? throw new FileNotFoundException("Build WileyWidget.WinForms first.");
        }

        private void EnsureAppBuilt()
        {
            var proc = Process.Start("dotnet", "build ../WileyWidget.WinForms/WileyWidget.WinForms.csproj --configuration Debug");
            proc?.WaitForExit(60000);
            if (proc?.ExitCode != 0)
            {
                throw new InvalidOperationException("Failed to build app – check errors.");
            }
        }

        public void Dispose()
        {
            ServiceProvider?.Dispose();
            FlaUITestBase.ForceCloseSharedApplication(); // Cleanup
        }
    }
}
