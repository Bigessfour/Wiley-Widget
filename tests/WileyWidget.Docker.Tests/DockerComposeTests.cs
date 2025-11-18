using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Xunit;

namespace WileyWidget.Docker.Tests;

public class DockerComposeTests
{
    [Fact]
    public async Task DockerComposeConfigIsValid()
    {
        // Arrange
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "config",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetFullPath("../../../") // Root directory
            }
        };

        // Act
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0, "docker-compose config should validate successfully");
        error.Should().BeEmpty("No validation errors should occur");
    }

    [Fact]
    public async Task DatabaseServiceHealthCheckWorks()
    {
        // This test would require starting the db service and checking health
        // For now, just validate the healthcheck configuration exists
        var composeContent = await File.ReadAllTextAsync("../../../docker-compose.yml");
        composeContent.Should().Contain("healthcheck", "docker-compose should have healthcheck for db service");
        composeContent.Should().Contain("test:", "healthcheck should have test command");
    }

    [Fact]
    public async Task VolumeMountsAreConfiguredCorrectly()
    {
        // Arrange
        var composeContent = await File.ReadAllTextAsync("../../../docker-compose.yml");

        // Act & Assert
        composeContent.Should().Contain("volumes:", "docker-compose should have volumes section");
        composeContent.Should().Contain("./coverage:/src/coverage:rw", "test service should mount coverage volume");
        composeContent.Should().Contain("./:/src", "services should mount source code");
    }

    [Fact]
    public async Task ServiceDependenciesAreCorrect()
    {
        // Arrange
        var composeContent = await File.ReadAllTextAsync("../../../docker-compose.yml");

        // Act & Assert
        composeContent.Should().Contain("depends_on:", "services should have dependencies");
        composeContent.Should().Contain("condition: service_healthy", "db dependencies should wait for healthy state");
    }
}