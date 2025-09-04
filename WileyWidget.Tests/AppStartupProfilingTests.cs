using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.Tests;

/// <summary>
/// Optional profiling test that runs the application multiple times with startup diagnostics enabled
/// and computes basic statistics for Time-To-First-Window (TTFW). Helps identify regressions and variance.
/// </summary>
public class AppStartupProfilingTests
{
    private const int Runs = 3; // keep small to limit test duration
    private const int TimeoutSeconds = 25;
    private const int ThresholdMs = 10000; // existing SLA threshold

    [Fact]
    [Trait("Category", "StartupProfiling")]
    public async Task TimeToFirstWindow_Profiled_MedianShouldBeUnderThreshold()
    {
        var repoRoot = GetRepoRoot();
        var logsDir = Path.Combine(repoRoot, "logs");
        Directory.CreateDirectory(logsDir);
        var metricsPath = Path.Combine(logsDir, "startup-metrics.log");

        var results = new List<long>();

        for (int i = 0; i < Runs; i++)
        {
            if (File.Exists(metricsPath)) File.Delete(metricsPath);

            var psi = new ProcessStartInfo(
                "dotnet",
                $"run --project \"{Path.Combine(repoRoot, "WileyWidget.csproj")}\" -- --diag-startup --ttfw-exit")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            Assert.NotNull(proc);

            var timeout = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds));
            var exited = Task.Run(() => proc!.WaitForExit());
            var finished = await Task.WhenAny(exited, timeout);
            if (finished == timeout)
            {
                try { proc.Kill(); } catch { }
                throw new TimeoutException($"Run {i + 1} did not exit within {TimeoutSeconds}s during TTFW profiling");
            }

            await Task.Delay(200); // small flush window

            if (!File.Exists(metricsPath))
            {
                var stdOut = proc?.StandardOutput.ReadToEnd() ?? string.Empty;
                var stdErr = proc?.StandardError.ReadToEnd() ?? string.Empty;
                throw new InvalidOperationException($"Run {i + 1}: startup-metrics.log not created. STDOUT=\n{Truncate(stdOut)}\nSTDERR=\n{Truncate(stdErr)}");
            }

            var lines = File.ReadAllLines(metricsPath);
            var firstWindow = lines.FirstOrDefault(l => l.Contains("FirstWindowShown"));
            Assert.False(string.IsNullOrEmpty(firstWindow), $"Run {i + 1}: FirstWindowShown event missing. Lines=\n{string.Join("\n", lines)}");

            var marker = "TTFW_MS=";
            var idx = firstWindow.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            Assert.True(idx > 0, $"Run {i + 1}: TTFW_MS marker missing: {firstWindow}");
            var start = idx + marker.Length;
            var end = start;
            while (end < firstWindow.Length && char.IsDigit(firstWindow[end])) end++;
            Assert.True(long.TryParse(firstWindow.Substring(start, end - start), out var ttfw), $"Run {i + 1}: Invalid TTFW value slice='{firstWindow.Substring(start)}'");
            Assert.True(ttfw > 0, $"Run {i + 1}: TTFW must be > 0");
            results.Add(ttfw);
        }

        results.Sort();
        var median = results[results.Count / 2];
        var average = (long)results.Average();
        var max = results.Max();

        Assert.True(median < ThresholdMs, $"Median TTFW {median}ms exceeds threshold {ThresholdMs}ms (avg={average} max={max} runs=[{string.Join(",", results)}])");
    }

    private static string Truncate(string s, int max = 800)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max) + "...<truncated>");

    private static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "WileyWidget.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException("Repository root not found");
    }
}
