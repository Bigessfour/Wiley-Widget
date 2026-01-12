using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Regression;

public class RegressionGuardsTests
{
    [Fact]
    public void NoManualColorOverrides()
    {
        // Find solution root by locating WileyWidget.sln
        var solutionRoot = FindSolutionRoot();

        var srcRoot = Path.Combine(solutionRoot, "src");
        var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
                             .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                             .ToList();

        var violations = new List<string>();

        foreach (var file in files)
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains("Color.FromArgb", StringComparison.Ordinal))
                {
                    violations.Add($"{file}({lineNumber}): {line.Trim()}");
                }
            }
        }

        if (violations.Any())
        {
            var message = $"Found {violations.Count} usages of Color.FromArgb across src/tests:\n" + string.Join("\n", violations.Take(50));
            Assert.False(violations.Any(), message);
        }
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any())
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Solution root (WileyWidget.sln) was not found from the test context.");
    }
}
