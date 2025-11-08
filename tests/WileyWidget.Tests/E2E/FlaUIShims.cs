using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Conditions;

namespace WileyWidget.Tests.E2E
{
    // Lightweight DataGrid shim for tests that expect AsDataGrid() and Rows[] access
    public class DataGridShim
    {
        public AutomationElement Element { get; }
        public DataGridShim(AutomationElement element)
        {
            Element = element;
        }

        public AutomationElement[] Rows =>
            Element.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).ToArray();

        public AutomationElement? FindFirstDescendant(Func<ConditionFactory, ConditionBase> by)
            => Element.FindFirstDescendant(by);
    }

    public static class FlaUITestExtensions
    {
        // Wait until predicate is true or timeout elapses
        public static void Wait(this AutomationElement element, TimeSpan timeout, Func<AutomationElement, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (predicate(element)) return;
                Thread.Sleep(100);
            }
        }

        // Provide AsDataGrid() used by tests; returns a shim for basic row traversal
        public static DataGridShim AsDataGrid(this AutomationElement element)
            => new DataGridShim(element);

        // Provide AsTextBox() for text input elements
        public static TextBoxShim AsTextBox(this AutomationElement element)
            => new TextBoxShim(element);
    }

    // Simple TextBox wrapper for text property access
    public class TextBoxShim
    {
        public AutomationElement Element { get; }
        public TextBoxShim(AutomationElement element)
        {
            Element = element;
        }

        public string Text
        {
            get => Element.AsTextBox().Text;
            set => Element.AsTextBox().Text = value;
        }
    }

    // Simple predicate holder used by tests (e.g., WindowCondition.IsEnabled)
    public static class WindowCondition
    {
        public static Func<AutomationElement, bool> IsEnabled => e => e?.IsEnabled ?? false;
    }
}
