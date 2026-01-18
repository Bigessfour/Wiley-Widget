using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Plain-language FAQ for the Syncfusion Chart Wizard.
    /// Intended to be reused by any chart surface in Wiley Widget.
    /// </summary>
    public sealed class ChartWizardFaqDialog : Form
    {
        private RichTextBox? _content;
        private SfButton? _okButton;

        private ChartWizardFaqDialog()
        {
            InitializeDialog();
            this.PerformLayout();
            this.Refresh();
        }

        /// <summary>
        /// Show the Chart Wizard FAQ as a modal dialog.
        /// </summary>
        public static void ShowModal(IWin32Window? owner)
        {
            using var dialog = new ChartWizardFaqDialog();
            if (owner != null)
            {
                dialog.ShowDialog(owner);
                return;
            }

            dialog.ShowDialog();
        }


        private void InitializeDialog()
        {
            Text = "Chart Wizard FAQ";
            Size = new Size(720, 640);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            _content = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                TabStop = false
            };
            _content.Text = BuildFaqText();

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _okButton = new SfButton
            {
                Text = "OK",
                Size = new Size(90, 32),
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };
            _okButton.Click += (s, e) => Close();

            buttonPanel.Controls.Add(_okButton);

            layout.Controls.Add(_content, 0, 0);
            layout.Controls.Add(buttonPanel, 0, 1);

            Controls.Add(layout);

            AcceptButton = _okButton;

            // Apply theme (cascades to all children)
            ThemeColors.ApplyTheme(this);
        }

        private static string BuildFaqText()
        {
            // NOTE: Keep this as plain, everyday language. Avoid technical terms.
            var sb = new StringBuilder();

            sb.AppendLine("Chart Wizard FAQ (easy directions)");
            sb.AppendLine();

            sb.AppendLine("What is the Chart Wizard?");
            sb.AppendLine("- It is a guided tool that helps you change how a chart looks.");
            sb.AppendLine("- Think of it like a step-by-step setup screen.");
            sb.AppendLine();

            sb.AppendLine("What can I change with it?");
            sb.AppendLine("Depending on the chart and the data, you may be able to:");
            sb.AppendLine("- Change the chart style (for example: bars/columns, lines, pie).");
            sb.AppendLine("- Change which values are shown.");
            sb.AppendLine("- Change titles, labels, and the legend (the key).");
            sb.AppendLine("- Change colors, patterns, and other visual settings.");
            sb.AppendLine();

            sb.AppendLine("Important notes");
            sb.AppendLine("- Not every chart supports every option.");
            sb.AppendLine("- Some screens may look a little different depending on your version.");
            sb.AppendLine("- If you do not see an option, it may be locked for that chart.");
            sb.AppendLine();

            sb.AppendLine("How do I open the Chart Wizard?");
            sb.AppendLine("1) Go to the chart.");
            sb.AppendLine("2) Right-click anywhere inside the chart.");
            sb.AppendLine("3) Click: Chart Wizard...");
            sb.AppendLine();
            sb.AppendLine("Visual hint:");
            sb.AppendLine("- Right-click chart -> Menu -> Chart Wizard...");
            sb.AppendLine();

            sb.AppendLine("How do I use the wizard (step by step)?");
            sb.AppendLine("1) Read the page you are on (top to bottom).");
            sb.AppendLine("2) Make ONE change at a time.");
            sb.AppendLine("3) Click Next to continue.");
            sb.AppendLine("4) If something looks wrong, click Back.");
            sb.AppendLine("5) When you are happy, click Finish.");
            sb.AppendLine();

            sb.AppendLine("What do the buttons usually mean?");
            sb.AppendLine("- Next: go forward");
            sb.AppendLine("- Back: go back");
            sb.AppendLine("- Finish: apply your changes");
            sb.AppendLine("- Cancel: close without applying");
            sb.AppendLine();

            sb.AppendLine("Common choices (plain language)");
            sb.AppendLine("- Chart style: The overall look (bars, lines, pie, etc.).");
            sb.AppendLine("- Titles: The words at the top or along the sides.");
            sb.AppendLine("- Labels: The names and numbers shown on the chart.");
            sb.AppendLine("- Legend: The box that explains what each color means.");
            sb.AppendLine();

            sb.AppendLine("Saving your chart look (recommended)");
            sb.AppendLine("After you finish the wizard, you can save the look so you can reuse it:");
            sb.AppendLine("1) Right-click the chart.");
            sb.AppendLine("2) Click: Save Template...");
            sb.AppendLine("3) Pick a file name and location, then Save.");
            sb.AppendLine();
            sb.AppendLine("Later, to reuse the look:");
            sb.AppendLine("1) Right-click the chart.");
            sb.AppendLine("2) Click: Load Template...");
            sb.AppendLine();
            sb.AppendLine("Tip: A template saves the LOOK (layout and styling). It does not change your numbers.");
            sb.AppendLine();

            sb.AppendLine("Resetting back to the default look");
            sb.AppendLine("If you want to undo your custom look:");
            sb.AppendLine("1) Right-click the chart.");
            sb.AppendLine("2) Click: Reset Template");
            sb.AppendLine();

            sb.AppendLine("Troubleshooting");
            sb.AppendLine("- Wizard will not open: Try closing the panel and reopening it.");
            sb.AppendLine("- I cannot find the option I want: That option may not be available for this chart.");
            sb.AppendLine("- The chart looks strange after changes: Use Reset Template and try again.");

            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content?.Dispose();
                _okButton?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
