using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace WileyWidget.UiTests;

// Placeholder for future automated UI tests.
// Potential frameworks: WinAppDriver, FlaUI, or Playwright (.NET) for window automation.
// Strategy: Launch compiled exe, perform smoke validations (window title, control presence), then close.
public static class UiSmoke
{
	public static bool LaunchAndClose()
	{
		var exePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
			"..","..","..","..","WileyWidget","bin","Debug","net9.0-windows","WileyWidget.exe");
		exePath = Path.GetFullPath(exePath);
		if (!File.Exists(exePath)) return false;
		var proc = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
		if (proc == null) return false;
		// Allow startup time (would be replaced with real UI automation wait logic)
		Thread.Sleep(1500);
		try { proc.CloseMainWindow(); } catch { /* ignore */ }
		proc.WaitForExit(3000);
		return proc.HasExited;
	}
}
