using System;
using System.IO;
using System.Linq;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Theming;

public class SyncfusionThemingRegressionTests
{
    [Fact]
    public void NoManualColorOverrides()
    {
        // Locate repository root by walking up from test bin folder
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("WileyWidget.sln").Any())
            dir = dir.Parent;
        Assert.NotNull(dir);

        var srcRoot = Path.Combine(dir.FullName, "src");
        Assert.True(Directory.Exists(srcRoot), $"Expected src folder at '{srcRoot}'");

        var colorFromArgbCount = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(File.ReadLines)
            .Count(line => line.Contains("Color.FromArgb", StringComparison.Ordinal));

        Assert.Equal(0, colorFromArgbCount);
    }
}
