using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(
        Path.GetTempPath(),
        "WileyWidget.SettingsServiceTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_AllowsNullQboTokenExpiryInPersistedSettings()
    {
        Directory.CreateDirectory(_settingsDirectory);
        var settingsPath = Path.Combine(_settingsDirectory, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "Theme": "Office2019Colorful",
              "QboAccessToken": "access-token",
              "QboRefreshToken": "refresh-token",
              "QboTokenExpiry": null
            }
            """,
            CancellationToken.None);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Settings:Directory"] = _settingsDirectory,
            })
            .Build();

        var sut = new SettingsService(configuration);

        await sut.LoadAsync();

        sut.Current.Theme.Should().Be("Office2019Colorful");
        sut.Current.QboAccessToken.Should().Be("access-token");
        sut.Current.QboRefreshToken.Should().Be("refresh-token");
        sut.Current.QboTokenExpiry.Should().Be(default);
    }

    [Fact]
    public async Task LoadAsync_WhenDefaultExportPathIsMissing_UsesModelDefault()
    {
        Directory.CreateDirectory(_settingsDirectory);
        var settingsPath = Path.Combine(_settingsDirectory, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "Theme": "Office2019Colorful"
            }
            """,
            CancellationToken.None);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Settings:Directory"] = _settingsDirectory,
            })
            .Build();

        var sut = new SettingsService(configuration);

        await sut.LoadAsync();

        sut.Current.DefaultExportPath.Should().NotBeNullOrWhiteSpace();
        sut.Current.DefaultExportPath.Should().EndWith(Path.Combine("WileyWidget", "Exports"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void LoadSettings_WhenQboTokenExpiryIsNull_UsesDefaultValueWithoutTreatingFileAsCorrupt()
    {
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "WileyWidget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var settingsPath = Path.Combine(settingsDirectory, "settings.json");
            File.WriteAllText(settingsPath, """
            {
              "Theme": "Office2019Colorful",
              "QboAccessToken": "token",
              "QboRefreshToken": "refresh-token",
              "QboTokenExpiry": null
            }
            """);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Settings:Directory"] = settingsDirectory
                })
                .Build();

            var service = new SettingsService(configuration);

            var loaded = service.LoadSettings();

            loaded.Theme.Should().Be("Office2019Colorful");
            loaded.QboAccessToken.Should().Be("token");
            loaded.QboRefreshToken.Should().Be("refresh-token");
            loaded.QboTokenExpiry.Should().Be(default);
            Directory.GetFiles(settingsDirectory, "*.bad_*").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, recursive: true);
            }
        }
    }
}
