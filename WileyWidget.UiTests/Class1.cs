using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using NUnit.Framework;

namespace WileyWidget.UiTests;

/// <summary>
/// Consolidated UI smoke tests ensuring the WPF app launches and shuts down cleanly.
/// Ensures processes are terminated to avoid orphaned dotnet hosts lingering after test run.
/// </summary>
[TestFixture]
public class UiSmokeTests
{
	private string _exePath = string.Empty; // initialized in OneTimeSetup
	private readonly List<int> _launchedPids = new();

	[OneTimeSetUp]
	public void OneTimeSetup()
	{
		_exePath = ResolveExe();
		if (!File.Exists(_exePath))
		{
			Assert.Inconclusive($"Executable not found at {_exePath}. Build the app first.");
		}
	}

	private static string ResolveExe()
	{
		var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
		var release = Path.GetFullPath(Path.Combine(baseDir, "..","..","..","..","WileyWidget","bin","Release","net9.0-windows","WileyWidget.exe"));
		if (File.Exists(release)) return release;
		var debug = Path.GetFullPath(Path.Combine(baseDir, "..","..","..","..","WileyWidget","bin","Debug","net9.0-windows","WileyWidget.exe"));
		return debug;
	}

	[Test, Category("UiSmokeTests")]
	[Apartment(System.Threading.ApartmentState.STA)]
	public void MainWindow_Launches_And_GridPresent()
	{
		Application app = null;
		UIA3Automation automation = null;
		try
		{
			var psi = new ProcessStartInfo(_exePath)
			{
				UseShellExecute = false
			};
			// Enable auto-close of Syncfusion trial dialog in app
			psi.Environment["WILEYWIDGET_AUTOCLOSE_LICENSE"] = "1";
			app = Application.Launch(psi);
			if (app != null) _launchedPids.Add(app.ProcessId);
			automation = new UIA3Automation();
			var window = Retry.WhileNull(() => app.GetMainWindow(automation), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250), throwOnTimeout: true).Result;
			Assert.That(window, Is.Not.Null, "Main window failed to load");
			Assert.That(window.Title, Does.Contain("Wiley"));
			// Try to find any child (sanity)
			var grid = Retry.WhileNull(() => window.FindFirstDescendant(e => e.ByControlType(FlaUI.Core.Definitions.ControlType.DataGrid)), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(150)).Result;
			Assert.That(grid, Is.Not.Null, "Expected a DataGrid control");
			// Close politely
			window.Close();
		}
		finally
		{
			try { automation?.Dispose(); } catch { }
			try
			{
				if (app != null && !app.HasExited)
				{
					app.Close();
					var waited = Retry.WhileTrue(() => !app.HasExited, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
					if (!app.HasExited)
					{
						app.Kill();
					}
				}
			}
			catch { }
		}
	}

	[TearDown]
	public void TearDown()
	{
		// Safety net: ensure any launched processes exit.
		foreach (var pid in _launchedPids.ToArray())
		{
			try
			{
				var proc = Process.GetProcessById(pid);
				if (proc is { HasExited: false })
				{
					if (!proc.WaitForExit(1500))
					{
						proc.Kill(entireProcessTree: true);
						proc.WaitForExit();
					}
				}
			}
			catch { }
			finally
			{
				_launchedPids.Remove(pid);
			}
		}
	}
}
