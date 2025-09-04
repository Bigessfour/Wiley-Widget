using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WileyWidget.Tests;

public class AppStartupTests
{
	[Fact]
	public async Task TimeToFirstWindow_ShouldBeCaptured_AndUnderThreshold()
	{
		var repoRoot = GetRepoRoot();
		var logsDir = Path.Combine(repoRoot, "logs");
		Directory.CreateDirectory(logsDir);
		var metricsFile = Path.Combine(logsDir, "startup-metrics.log");
		if (File.Exists(metricsFile)) File.Delete(metricsFile);

		var psi = new ProcessStartInfo("dotnet", "run --project \"" + Path.Combine(repoRoot, "WileyWidget.csproj") + "\" -- --diag-startup --ttfw-exit")
		{
			WorkingDirectory = repoRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		var proc = Process.Start(psi);
		Assert.NotNull(proc);
		var timeout = Task.Delay(TimeSpan.FromSeconds(20));
		var exited = Task.Run(() => proc!.WaitForExit());
		var finished = await Task.WhenAny(exited, timeout);
		if (finished == timeout)
		{
			try { proc.Kill(); } catch { }
			throw new TimeoutException("Process did not exit within 20s during TTFW harness");
		}

		await Task.Delay(200); // allow flush

		Assert.True(File.Exists(metricsFile), "startup-metrics.log not created");
		var lines = File.ReadAllLines(metricsFile);
		var firstWindow = lines.FirstOrDefault(l => l.Contains("FirstWindowShown"));
		Assert.False(string.IsNullOrEmpty(firstWindow), "FirstWindowShown event missing");
		var marker = "TTFW_MS=";
		var idx = firstWindow.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		Assert.True(idx > 0, "TTFW_MS marker missing");
		var start = idx + marker.Length;
		var end = start;
		while (end < firstWindow.Length && char.IsDigit(firstWindow[end])) end++;
		Assert.True(long.TryParse(firstWindow.Substring(start, end - start), out var ttfw), "Invalid TTFW value");
		Assert.True(ttfw > 0, "TTFW must be > 0");
		Assert.True(ttfw < 10000, $"TTFW exceeded threshold: {ttfw}ms");
	}

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

