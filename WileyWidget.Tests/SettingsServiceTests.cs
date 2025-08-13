using System;
using System.IO;
using NUnit.Framework;
using WileyWidget.Services;

namespace WileyWidget.Tests;

[TestFixture]
public class SettingsServiceTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WileyWidgetTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR", _tempDir);
        SettingsService.Instance.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        Environment.SetEnvironmentVariable("WILEYWIDGET_SETTINGS_DIR", null);
    }

    [Test]
    public void SaveThenLoad_PersistsTheme()
    {
        SettingsService.Instance.Current.Theme = "FluentLight";
        SettingsService.Instance.Save();
        SettingsService.Instance.Current.Theme = "FluentDark"; // mutate in memory
        SettingsService.Instance.Load();
        Assert.That(SettingsService.Instance.Current.Theme, Is.EqualTo("FluentLight"));
    }

    [Test]
    public void CorruptFile_RenamesAndRecreates()
    {
        SettingsService.Instance.Save();
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{ this is not valid json }");
        SettingsService.Instance.Load();
        var badFiles = Directory.GetFiles(_tempDir, "settings.json.bad_*");
        Assert.That(badFiles.Length, Is.GreaterThanOrEqualTo(1));
        Assert.That(File.Exists(settingsPath), Is.True);
    }

    [Test]
    public void SaveThenLoad_PersistsQboTokens()
    {
        var now = DateTime.UtcNow.AddMinutes(55); // pretend expiry
        SettingsService.Instance.Current.QboAccessToken = "access-123";
        SettingsService.Instance.Current.QboRefreshToken = "refresh-456";
        SettingsService.Instance.Current.QboTokenExpiry = now;
        SettingsService.Instance.Save();

        // Reset in-memory and reload from disk
        SettingsService.Instance.ResetForTests();
        SettingsService.Instance.Load();

        Assert.Multiple(() =>
        {
            Assert.That(SettingsService.Instance.Current.QboAccessToken, Is.EqualTo("access-123"));
            Assert.That(SettingsService.Instance.Current.QboRefreshToken, Is.EqualTo("refresh-456"));
            Assert.That(SettingsService.Instance.Current.QboTokenExpiry, Is.EqualTo(now));
        });
    }
}
