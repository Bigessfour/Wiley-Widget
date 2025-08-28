using System.IO;
using Xunit;
using WileyWidget.Services;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Tests for the SettingsService singleton, file operations, and migration logic
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _originalRoot;

    public SettingsServiceTests()
    {
        // Create a temporary directory for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "WileyWidgetTest");
        Directory.CreateDirectory(_testDirectory);

        // Store original root and set test root
        _originalRoot = typeof(SettingsService)
            .GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(SettingsService.Instance) as string ?? "";

        // Use reflection to set the test directory
        typeof(SettingsService)
            .GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(SettingsService.Instance, _testDirectory);
    }

    [Fact]
    public void SettingsService_Instance_IsSingleton()
    {
        // Arrange
        var instance1 = SettingsService.Instance;
        var instance2 = SettingsService.Instance;

        // Assert
        Assert.Same(instance1, instance2);
        Assert.NotNull(instance1);
    }

    [Fact]
    public void SettingsService_Current_IsNotNull()
    {
        // Act
        var current = SettingsService.Instance.Current;

        // Assert
        Assert.NotNull(current);
        Assert.IsType<AppSettings>(current);
    }

    [Fact]
    public void SettingsService_ResetForTests_Works()
    {
        // Arrange - Modify current settings
        SettingsService.Instance.Current.Theme = "ModifiedTheme";
        SettingsService.Instance.Current.WindowWidth = 999;

        // Act - Reset for tests
        SettingsService.Instance.ResetForTests();

        // Assert - Should be back to defaults
        Assert.Equal("FluentDark", SettingsService.Instance.Current.Theme);
        Assert.Null(SettingsService.Instance.Current.WindowWidth);
    }

    [Fact]
    public void SettingsService_Save_CreatesFile()
    {
        // Arrange
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Current.Theme = "Dark";
        SettingsService.Instance.Current.WindowWidth = 800;
        SettingsService.Instance.Current.WindowHeight = 600;

        // Get the expected file path
        var filePath = Path.Combine(_testDirectory, "settings.json");

        // Act
        SettingsService.Instance.Save();

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void SettingsService_Load_ReadsFileCorrectly()
    {
        // Arrange - Create a test settings file
        var testSettings = new AppSettings
        {
            Theme = "Light",
            WindowWidth = 1024,
            WindowHeight = 768
        };

        var filePath = Path.Combine(_testDirectory, "settings.json");
        var json = System.Text.Json.JsonSerializer.Serialize(testSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        // Act - Reset and load
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        // Assert
        Assert.Equal("Light", SettingsService.Instance.Current.Theme);
        Assert.Equal(1024, SettingsService.Instance.Current.WindowWidth);
        Assert.Equal(768, SettingsService.Instance.Current.WindowHeight);
    }

    [Fact]
    public void SettingsService_Load_HandlesCorruptFile()
    {
        // Arrange - Create a corrupt JSON file
        var filePath = Path.Combine(_testDirectory, "settings.json");
        File.WriteAllText(filePath, "{ invalid json content }");

        // Act - Reset and load (should handle corruption gracefully)
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        // Assert - Should have default settings, not crash
        Assert.NotNull(SettingsService.Instance.Current);
        Assert.Equal("FluentDark", SettingsService.Instance.Current.Theme);
    }

    [Fact]
    public void SettingsService_Load_CreatesBackupOfCorruptFile()
    {
        // Arrange - Create a corrupt JSON file
        var filePath = Path.Combine(_testDirectory, "settings.json");
        File.WriteAllText(filePath, "{ invalid json content }");

        // Act - Load (should create backup)
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        // Assert - Should have created a backup file
        var backupFiles = Directory.GetFiles(_testDirectory, "settings.json.bad_*");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public void SettingsService_Migration_QuickBooksToQbo_Works()
    {
        // Arrange - Create settings with old QuickBooks properties
        var testSettings = new AppSettings
        {
            QuickBooksAccessToken = "old_token",
            QuickBooksRefreshToken = "old_refresh",
            QuickBooksTokenExpiresUtc = DateTime.UtcNow.AddHours(1)
        };

        var filePath = Path.Combine(_testDirectory, "settings.json");
        var json = System.Text.Json.JsonSerializer.Serialize(testSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        // Act - Load (should migrate old properties)
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        // Assert - Should have migrated to new QBO properties
        Assert.Equal("old_token", SettingsService.Instance.Current.QboAccessToken);
        Assert.Equal("old_refresh", SettingsService.Instance.Current.QboRefreshToken);
        Assert.True(SettingsService.Instance.Current.QboTokenExpiry > DateTime.UtcNow);
    }

    [Fact]
    public void SettingsService_Load_HandlesMissingFile()
    {
        // Arrange - Ensure no settings file exists
        var filePath = Path.Combine(_testDirectory, "settings.json");
        if (File.Exists(filePath))
            File.Delete(filePath);

        // Act - Load from non-existent file
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        // Assert - Should have default settings
        Assert.NotNull(SettingsService.Instance.Current);
        Assert.Equal("FluentDark", SettingsService.Instance.Current.Theme);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        // Restore original root
        typeof(SettingsService)
            .GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(SettingsService.Instance, _originalRoot);
    }
}
