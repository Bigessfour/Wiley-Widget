using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;

using FlaUIApp = FlaUI.Core.Application;

namespace WileyWidget.UiTests
{
    public static class FlaUiHelpers
    {
        public static Window WaitForMainWindow(FlaUIApp app, UIA2Automation automation, TimeSpan timeout)
        {
            return PanelActivationHelpers.WaitForMainWindow(app, automation, timeout);
        }

        public static void TryWaitForInputIdle(FlaUIApp app, TimeSpan timeout)
        {
            try
            {
                Process.GetProcessById(app.ProcessId).WaitForInputIdle((int)timeout.TotalMilliseconds);
            }
            catch
            {
            }
        }

        public static void ShutdownApp(FlaUIApp? app)
        {
            if (app == null)
            {
                return;
            }

            try
            {
                app.Close();
            }
            catch
            {
            }

            var hasExited = true;
            try
            {
                hasExited = app.HasExited;
            }
            catch
            {
                try
                {
                    hasExited = Process.GetProcessById(app.ProcessId).HasExited;
                }
                catch
                {
                    hasExited = true;
                }
            }

            if (!hasExited)
            {
                try
                {
                    app.Kill();
                }
                catch
                {
                    try
                    {
                        Process.GetProcessById(app.ProcessId).Kill(true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static string? TryGetName(AutomationElement element)
        {
            try
            {
                return element.Properties.Name.ValueOrDefault;
            }
            catch
            {
                return null;
            }
        }

        public static string? TryGetAutomationId(AutomationElement element)
        {
            try
            {
                return element.Properties.AutomationId.ValueOrDefault;
            }
            catch
            {
                return null;
            }
        }

        public static string TryGetControlType(AutomationElement element)
        {
            try
            {
                return element.Properties.ControlType.ValueOrDefault.ToString();
            }
            catch
            {
                return "?";
            }
        }

        public static AutomationElement? FindElementByName(Window window, string name, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var element = window.FindFirstDescendant(cf => cf.ByName(name));
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            return null;
        }

        public static AutomationElement? FindElementByNameOrId(Window window, string name, string automationId, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var element = window.FindFirstDescendant(cf => cf.ByName(name).Or(cf.ByAutomationId(automationId)));
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            return null;
        }

        public static void WaitForElement(Func<AutomationElement?> finder, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (finder() != null)
                {
                    return;
                }

                Thread.Sleep(150);
            }

            throw new TimeoutException($"Element not found within {timeout.TotalSeconds}s");
        }

        public static void DumpUiTree(Window window, string? filename = null, int maxDepth = 6)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var pid = Process.GetCurrentProcess().Id;

                // Ensure TestResults directory exists relative to current working directory
                var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
                try { Directory.CreateDirectory(resultsDir); } catch { }

                if (string.IsNullOrWhiteSpace(filename))
                {
                    filename = Path.Combine(resultsDir, $"ui-tree-{timestamp}-{pid}.log");
                }
                else if (!Path.IsPathRooted(filename))
                {
                    filename = Path.Combine(resultsDir, filename);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"UI Tree Dump - {DateTime.Now:O}");
                try { sb.AppendLine($"Window: {TryGetName(window) ?? "<no-name>"}"); } catch { }
                sb.AppendLine($"ProcessId: {Process.GetCurrentProcess().Id}");
                sb.AppendLine();

                void DumpElement(AutomationElement el, int depth)
                {
                    if (el == null) return;
                    if (depth > maxDepth) return;

                    var indent = new string(' ', depth * 2);
                    string name = string.Empty;
                    string aid = string.Empty;
                    string ctrl = string.Empty;
                    string cls = string.Empty;
                    string fw = string.Empty;
                    string help = string.Empty;
                    string bounds = string.Empty;
                    string enabled = "?";
                    string offscreen = "?";

                    try { name = TryGetName(el) ?? el.Name ?? string.Empty; } catch { name = el.Name ?? string.Empty; }
                    try { aid = TryGetAutomationId(el) ?? el.AutomationId ?? string.Empty; } catch { aid = el.AutomationId ?? string.Empty; }
                    try { ctrl = TryGetControlType(el); } catch { ctrl = "?"; }
                    try { cls = el.Properties.ClassName.ValueOrDefault ?? string.Empty; } catch { }
                    try { fw = el.Properties.FrameworkId.ValueOrDefault ?? string.Empty; } catch { }
                    try { help = el.Properties.HelpText.ValueOrDefault ?? string.Empty; } catch { }
                    try { var r = el.Properties.BoundingRectangle.Value; bounds = $"{r.Left:F0},{r.Top:F0},{r.Width:F0}x{r.Height:F0}"; } catch { }
                    try { enabled = el.IsEnabled ? "True" : "False"; } catch { }
                    try { offscreen = el.Properties.IsOffscreen.ValueOrDefault ? "True" : "False"; } catch { }

                    sb.AppendLine($"{indent}- {ctrl} Name='{name}' Id='{aid}' Class='{cls}' FW='{fw}' Enabled={enabled} Offscreen={offscreen}");
                    if (!string.IsNullOrEmpty(bounds)) sb.AppendLine($"{indent}  Bounds: {bounds}");
                    if (!string.IsNullOrWhiteSpace(help)) sb.AppendLine($"{indent}  HelpText: {help}");
                    try { sb.AppendLine($"{indent}  ToString: {el.ToString()}"); } catch { }

                    // Include common pattern values when available
                    try
                    {
                        var valuePattern = el.Patterns.Value;
                        if (valuePattern.IsSupported)
                        {
                            var val = valuePattern.Pattern.Value.Value ?? string.Empty;
                            sb.AppendLine($"{indent}  Value: {val}");
                        }
                    }
                    catch { }

                    try
                    {
                        var toggle = el.Patterns.Toggle;
                        if (toggle.IsSupported)
                        {
                            var tstate = toggle.Pattern.ToggleState.Value;
                            sb.AppendLine($"{indent}  ToggleState: {tstate}");
                        }
                    }
                    catch { }

                    try
                    {
                        var children = el.FindAllChildren();
                        foreach (var child in children)
                        {
                            DumpElement(child, depth + 1);
                        }
                    }
                    catch { }
                }

                try
                {
                    DumpElement(window, 0);
                }
                catch { }

                File.WriteAllText(filename, sb.ToString());
                Console.WriteLine($"[FlaUI DEBUG] UI tree dumped to {filename}");
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[FlaUI DEBUG] Failed to write UI tree: {ex}"); } catch { }
            }
        }

        public static Window? WaitForDialogByTitle(FlaUIApp app, UIA2Automation automation, string title, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    foreach (var window in app.GetAllTopLevelWindows(automation))
                    {
                        if (TryGetName(window)?.Contains(title, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return window;
                        }
                    }
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            return null;
        }
    }
}
