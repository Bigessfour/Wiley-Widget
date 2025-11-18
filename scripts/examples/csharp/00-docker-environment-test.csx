#!/usr/bin/env dotnet-script
#r "nuget: FluentAssertions, 6.8.0"
#r "nuget: System.Diagnostics.Process, 4.3.0"

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;

// Test 1: Verify dotnet-script is available
Console.WriteLine("Testing dotnet-script availability...");
var dotnetScriptVersion = RunCommand("dotnet tool list --global");
dotnetScriptVersion.Should().Contain("dotnet-script", "dotnet-script should be installed globally");

// Test 2: Verify .NET SDK version
Console.WriteLine("Testing .NET SDK version...");
var dotnetVersion = RunCommand("dotnet --version");
dotnetVersion.Should().NotBeNullOrEmpty(".NET SDK should be available");
Console.WriteLine($".NET Version: {dotnetVersion.Trim()}");

// Test 3: Verify NuGet cache is accessible
Console.WriteLine("Testing NuGet cache accessibility...");
var nugetCachePath = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
Directory.Exists(nugetCachePath).Should().BeTrue("NuGet cache directory should exist");
Console.WriteLine($"NuGet Cache: {nugetCachePath}");

// Test 4: Test package reference resolution
Console.WriteLine("Testing package reference resolution...");
try
{
    // This will test if NuGet packages can be resolved
    var testAssembly = typeof(FluentAssertions.AssertionExtensions).Assembly;
    testAssembly.Should().NotBeNull("FluentAssertions package should be resolvable");
    Console.WriteLine("Package resolution: SUCCESS");
}
catch (Exception ex)
{
    throw new Exception($"Package resolution failed: {ex.Message}");
}

// Test 5: Verify environment variables
Console.WriteLine("Testing environment variables...");
var repoRoot = Environment.GetEnvironmentVariable("WW_REPO_ROOT");
repoRoot.Should().NotBeNullOrEmpty("WW_REPO_ROOT should be set");
Directory.Exists(repoRoot).Should().BeTrue("WW_REPO_ROOT should point to existing directory");

var logsDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR");
logsDir.Should().NotBeNullOrEmpty("WW_LOGS_DIR should be set");
// Note: logs dir might not exist yet, that's ok

// Test 6: Test file system access
Console.WriteLine("Testing file system access...");
var testFile = Path.Combine(repoRoot, "README.md");
File.Exists(testFile).Should().BeTrue("Should be able to access repository files");

// Test 7: Test process execution
Console.WriteLine("Testing process execution...");
var whoami = RunCommand("whoami");
whoami.Should().NotBeNullOrEmpty("Should be able to run system commands");

Console.WriteLine("All Docker environment tests passed! ✅");

string RunCommand(string command)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };

    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new Exception($"Command failed: {command}\nError: {error}");
    }

    return output;
}