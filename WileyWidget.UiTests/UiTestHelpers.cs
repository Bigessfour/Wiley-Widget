using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using System.Threading;

namespace WileyWidget.UiTests
{
    public static class UiTestHelpers
    {
        public static Window GetMainWindow(Application app, UIA3Automation automation, TimeSpan timeout)
        {
            var w = app.GetMainWindow(automation, timeout);
            if (w != null) return w;

            var desktop = automation.GetDesktop();
            w = desktop.FindFirstDescendant(cf => cf.ByName("Wiley").And(cf.ByControlType(ControlType.Window)))?.AsWindow();
            if (w != null) return w;

            var windows = automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(app.ProcessId).And(cf.ByControlType(ControlType.Window)));
            return windows.FirstOrDefault()?.AsWindow();
        }

        public static AutomationElement? RetryFindByAutomationId(Window root, string automationId, TimeSpan timeout)
        {
            return Retry.WhileNull(() => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout, TimeSpan.FromMilliseconds(200)).Result;
        }

        public static int TryGetRowCount(AutomationElement grid)
        {
            var gp = grid.Patterns.Grid;
            if (gp.IsSupported)
            {
                var rc = gp.Pattern.RowCount ?? 0;
                if (rc > 0) return rc;
            }

            var tp = grid.Patterns.Table;
            if (tp.IsSupported)
            {
                var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                if (rows?.Length > 0) return rows.Length;
            }

            var items = grid.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem));
            return items?.Length ?? 0;
        }

        public static int WaitForRowCount(AutomationElement grid, int expectedMinimum, TimeSpan timeout)
        {
            var result = Retry.WhileNull(() =>
            {
                var rc = TryGetRowCount(grid);
                return rc >= expectedMinimum ? (int?)rc : null;
            }, timeout, TimeSpan.FromMilliseconds(200));
            return result.Result ?? 0;
        }

        public static int? WaitForPredicate(Func<int?> predicate, TimeSpan timeout)
        {
            var result = Retry.WhileNull(() => predicate(), timeout, TimeSpan.FromMilliseconds(200));
            return result.Result;
        }

        public static string CaptureElementToTempFile(AutomationElement el)
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"ui_{Guid.NewGuid():N}.png");
            Capture.Element(el).ToFile(tmp);
            return tmp;
        }

        public static Color SampleElementCenterColor(AutomationElement el)
        {
            var file = CaptureElementToTempFile(el);
            try
            {
                using var bmp = new Bitmap(file);
                var c = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                return c;
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }

        public static AutomationElement? GetHeaderByName(AutomationElement grid, string headerName)
        {
            // Search for header descendants first
            var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header).Or(cf.ByControlType(ControlType.HeaderItem)));
            var candidate = headers.FirstOrDefault(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase));
            if (candidate != null) return candidate;

            // Fallback: search any descendant with the header name
            return grid.FindFirstDescendant(cf => cf.ByName(headerName));
        }

        public static int GetColumnIndexByHeader(AutomationElement grid, string headerName)
        {
            var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header).Or(cf.ByControlType(ControlType.HeaderItem)));
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i].Name, headerName, StringComparison.OrdinalIgnoreCase)) return i;
            }

            // fallback: attempt to find header items by name anywhere and return index of matching among header-like elements
            var allHeaderLike = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text).Or(cf.ByControlType(ControlType.Button)));
            for (int i = 0; i < allHeaderLike.Length; i++)
            {
                if (string.Equals(allHeaderLike[i].Name, headerName, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        public static AutomationElement? GetCellByRowCol(AutomationElement grid, int rowIndex, int colIndex)
        {
            var gp = grid.Patterns.Grid;
            if (gp.IsSupported)
            {
                try
                {
                    var cell = gp.Pattern.GetItem(rowIndex, colIndex);
                    if (cell != null) return cell;
                }
                catch { /* get item may throw if virtualization hides item */ }
            }

            // Fallback: use DataItem collection and text descendants to approximate
            var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            if (rows == null || rows.Length == 0) return null;
            if (rowIndex < 0 || rowIndex >= rows.Length) return null;
            var row = rows[rowIndex];
            var texts = row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            if (texts != null && texts.Length > colIndex) return texts[colIndex].Parent ?? texts[colIndex];
            return texts?.FirstOrDefault();
        }

        public static void ScrollCellIntoView(AutomationElement cell)
        {
            if (cell == null) return;
            var sip = cell.Patterns.ScrollItem;
            if (sip.IsSupported)
            {
                try { sip.Pattern.ScrollIntoView(); } catch { }
            }
            else
            {
                try { cell.Focus(); } catch { }
            }
        }

        // Expand a ComboBox and select an item by name
        public static bool ExpandComboAndSelect(Window root, string comboAutomationId, string itemName, TimeSpan timeout)
        {
            var combo = Retry.WhileNull(() => root.FindFirstDescendant(cf => cf.ByAutomationId(comboAutomationId)), timeout, TimeSpan.FromMilliseconds(200)).Result;
            if (combo == null) return false;
            var cb = combo.AsComboBox();
            try
            {
                cb.Expand();
                Thread.Sleep(200);
                var item = cb.Items.FirstOrDefault(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    // try to find in descendants
                    item = combo.FindFirstDescendant(cf => cf.ByName(itemName));
                }
                if (item == null) return false;
                item.Click();
                return true;
            }
            catch { return false; }
        }

        // Set value on a text box by automation id or element
        public static void SetTextBoxValue(AutomationElement? el, string text)
        {
            if (el == null) return;
            try
            {
                var tb = el.AsTextBox();
                if (tb != null)
                {
                    tb.Text = text;
                    return;
                }
            }
            catch { }

            // fallback to ValuePattern
            var vp = el.Patterns.Value;
            if (vp.IsSupported)
            {
                try { vp.Pattern.SetValue(text); } catch { }
            }
        }

        // Double click an element
        public static void DoubleClick(AutomationElement el)
        {
            if (el == null) return;
            try
            {
                var rect = el.BoundingRectangle;
                var pt = rect.Center();
                Mouse.MoveTo(pt);
                Mouse.DoubleClick(pt);
            }
            catch
            {
                try { el.Click(); Thread.Sleep(100); el.Click(); } catch { }
            }
        }

        // Type text using keyboard and optionally press Enter
        public static void TypeText(string text, bool pressEnter = false)
        {
            Keyboard.Type(text);
            if (pressEnter) Keyboard.Press(VirtualKeyShort.RETURN);
        }

        // Wait for a top-level window whose title contains given text
        public static AutomationElement? WaitForTopLevelWindowWithTitle(string titleContains, TimeSpan timeout)
        {
            var desktop = AutomationElement.RootElement;
            var res = Retry.WhileNull(() =>
            {
                var win = desktop.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window).And(cf.ByName(s => s != null && s.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0)));
                return win;
            }, timeout, TimeSpan.FromMilliseconds(200));
            return res.Result;
        }

        // Wait for tooltip or text pattern containing text anywhere under root
        public static AutomationElement? WaitForTooltipText(Window root, string textContains, TimeSpan timeout)
        {
            var res = Retry.WhileNull(() =>
            {
                var el = root.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(s => s != null && s.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0)));
                return el;
            }, timeout, TimeSpan.FromMilliseconds(200));
            return res.Result;
        }

        // Return string values for a column identified by headerName. Attempts to discover column index and read each DataItem's text at that index.
        public static string[] GetColumnStringsByHeader(AutomationElement grid, string headerName)
        {
            var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header).Or(cf.ByControlType(ControlType.HeaderItem)));
            int colIndex = -1;
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i].Name, headerName, StringComparison.OrdinalIgnoreCase)) { colIndex = i; break; }
            }

            var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            if (rows == null || rows.Length == 0) return Array.Empty<string>();
            if (colIndex >= 0)
            {
                return rows.Select(r =>
                {
                    var texts = r.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                    if (texts != null && texts.Length > colIndex) return texts[colIndex].Name ?? string.Empty;
                    return texts?.FirstOrDefault()?.Name ?? string.Empty;
                }).ToArray();
            }

            // fallback: try to map by header ordering via first row's text count
            return rows.Select(r => r.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).FirstOrDefault()?.Name ?? string.Empty).ToArray();
        }

        // Measure execution time of an action (synchronous) and return elapsed ms
        public static long MeasureActionMilliseconds(Action action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Find duplicates and nulls in a sequence
        public static (bool hasNullOrEmpty, bool hasDuplicates, string[] duplicates) AnalyzeForNullsAndDuplicates(string[] values)
        {
            var hasNull = values.Any(s => string.IsNullOrWhiteSpace(s));
            var groups = values.Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(s => s).Where(g => g.Count() > 1).ToArray();
            var dupKeys = groups.Select(g => g.Key).ToArray();
            return (hasNull, dupKeys.Length > 0, dupKeys);
        }

        // Cycle through focus using Tab key up to maxSteps and collect Name properties of focused elements
        public static string[] CycleTabCollectFocusNames(Window root, int maxSteps)
        {
            var list = new System.Collections.Generic.List<string>();
            for (int i = 0; i < maxSteps; i++)
            {
                Keyboard.Press(VirtualKeyShort.TAB);
                Thread.Sleep(120);
                var focused = root.Automation.GetFocusedElement();
                if (focused != null)
                {
                    var name = focused.Name ?? focused.AutomationId ?? string.Empty;
                    list.Add(name);
                }
            }
            return list.ToArray();
        }

        // Verify that the grid Name (accessible name) matches expected
        public static bool CheckAccessibleName(AutomationElement el, string expected)
        {
            if (el == null) return false;
            var name = el.Name ?? string.Empty;
            return string.Equals(name.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // Resize window to specified width/height (attempts to set via WindowPattern)
        public static void ResizeWindow(Window window, int width, int height)
        {
            if (window == null) return;
            try
            {
                window.Move(window.BoundingRectangle.X, window.BoundingRectangle.Y);
                window.Resize(width, height);
                Thread.Sleep(200);
            }
            catch { try { window.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal); } catch { } }
        }

        // Try to close app gracefully; does not throw
        public static void TryCloseAppGracefully(Application app, UIA3Automation automation, int timeoutSeconds = 2)
        {
            if (app == null) return;
            try
            {
                if (!app.HasExited)
                {
                    app.Close();
                    app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(timeoutSeconds));
                    if (!app.HasExited)
                    {
                        app.Kill();
                    }
                }
            }
            catch { try { app.Kill(); } catch { } }
            finally
            {
                try { automation?.Dispose(); } catch { }
            }
        }

        // Prefer Grid/Table pattern for SfDataGrid if available
        // public static FlaUI.Core.AutomationPattern? GetPreferredGridPattern(AutomationElement grid)
        // {
        //     var gp = grid.Patterns.Grid;
        //     if (gp.IsSupported) return gp.Pattern;
        //     var tp = grid.Patterns.Table;
        //     if (tp.IsSupported) return tp.Pattern;
        //     return null;
        // }

        // Poll for Prism region activation by name
        public static AutomationElement? WaitForRegionActivation(Window root, string regionName, TimeSpan timeout)
        {
            return Retry.WhileNull(() => root.FindFirstDescendant(cf => cf.ByName(regionName)), timeout, TimeSpan.FromMilliseconds(200)).Result;
        }

        public static string GetCellText(AutomationElement cell)
        {
            var txt = cell.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text))?.Name;
            if (!string.IsNullOrWhiteSpace(txt)) return txt.Trim();

            // fallback: try value pattern
            var vp = cell.Patterns.Value;
            if (vp.IsSupported) return vp.Pattern.Value ?? string.Empty;
            return string.Empty;
        }

        public static void CaptureOnFailure(string name, Application app, UIA3Automation automation)
        {
            try
            {
                var main = GetMainWindow(app, automation, TimeSpan.FromSeconds(5));
                if (main != null)
                {
                    var repo = AppContext.BaseDirectory;
                    var outDir = Path.Combine(repo, "TestResults", "Screenshots");
                    Directory.CreateDirectory(outDir);
                    var file = Path.Combine(outDir, $"{name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
                    Capture.Element(main).ToFile(file);
                }
            }
            catch { }
        }
    }
}
