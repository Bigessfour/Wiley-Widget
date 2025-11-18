using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace WileyWidget.Docker.Tests;

public class DockerImageTests
{
    [Fact]
    public async Task CsxTestImageBuildsSuccessfully()
    {
        // Arrange
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "build -t wiley-widget/csx-mcp:local docker/ -f docker/Dockerfile.csx-tests",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        // Act
        process.Start();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0, "Docker image should build successfully");
    }

    [Fact]
    public async Task TestsImageBuildsSuccessfully()
    {
        // Arrange
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "build -t wiley-tests docker/ -f docker/Dockerfile.tests",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        // Act
        process.Start();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0, "Docker test image should build successfully");
    }

    [Fact]
    public async Task DotnetScriptToolIsAvailableInCsxImage()
    {
        // Arrange
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "run --rm wiley-widget/csx-mcp:local dotnet tool list --global",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        // Act
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Assert
        process.ExitCode.Should().Be(0, "Docker run should succeed");
        output.Should().Contain("dotnet-script", "dotnet-script tool should be installed globally");
    }
}