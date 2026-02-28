using System.Text.RegularExpressions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Theming;

public sealed class ThemeColorPolicyTests
{
    [Fact]
    public void WinFormsSource_HasNoManualBackColorOrNonSemanticForeColor()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var srcRoot = Path.Combine(repoRoot, "src", "WileyWidget.WinForms");

        var files = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("ControlSafeExtensions.cs", StringComparison.OrdinalIgnoreCase));

        var violations = new List<string>();
        var backColorRegex = new Regex(@"\bBackColor\s*=", RegexOptions.Compiled);
        var foreColorRegex = new Regex(@"\bForeColor\s*=", RegexOptions.Compiled);
        var semanticForeRegex = new Regex(@"ThemeColors\.(Success|Error|Warning)\b", RegexOptions.Compiled);

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (backColorRegex.IsMatch(line))
                {
                    violations.Add($"{file}:{i + 1}: {line.Trim()}");
                    continue;
                }

                if (foreColorRegex.IsMatch(line) && !semanticForeRegex.IsMatch(line))
                {
                    violations.Add($"{file}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0, "Theme color policy violations:\n" + string.Join(Environment.NewLine, violations));
    }
}
