using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.Controls;
using WileyWidget.WinForms.Factories;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Dialogs
{
    /// <summary>
    /// Plain-language walkthrough for the Budget panel workflow.
    /// </summary>
    public sealed class BudgetHelpForm : SfForm
    {
        private RichTextBox? _walkthroughBox;

        public BudgetHelpForm()
        {
            InitializeDialog();
            PerformLayout();
            Refresh();
        }

        private void InitializeDialog()
        {
            Text = "Budget Panel — How It All Works";
            Size = new Size(800, 620);
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
                RowCount = 1,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _walkthroughBox = CreateWalkthroughBox();
            _walkthroughBox.Rtf = BuildWalkthroughRtf();

            layout.Controls.Add(_walkthroughBox, 0, 0);
            Controls.Add(layout);

            ThemeColors.ApplyTheme(this);
        }

        private static RichTextBox CreateWalkthroughBox()
        {
            var controlFactory = ServiceProviderServiceExtensions.GetService<SyncfusionControlFactory>(Program.Services);
            if (controlFactory?.CreateRichTextBoxExt(ConfigureWalkthroughBox) is RichTextBox richTextBox)
            {
                return richTextBox;
            }

            var fallback = new RichTextBox();
            ConfigureWalkthroughBox(fallback);
            return fallback;
        }

        private static void ConfigureWalkthroughBox(RichTextBox richTextBox)
        {
            richTextBox.Dock = DockStyle.Fill;
            richTextBox.ReadOnly = true;
            richTextBox.DetectUrls = false;
            richTextBox.BorderStyle = BorderStyle.None;
            richTextBox.WordWrap = true;
            richTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            richTextBox.TabStop = false;
            richTextBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        }

        private static string BuildWalkthroughRtf()
        {
            return @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}
\f0\fs20
{\b\fs32 Budget Panel - Quick Tour}\par\par
{\b 1. The Big Picture}\par
This panel shows your full town budget with actual spending pulled automatically from payments.\par
When PaymentsPanel saves a transaction with the right MunicipalAccountId, the matching budget account updates here.\par\par

{\b 2. CSV Mapping Wizard (Importing Deb's Chart of Accounts)}\par
- Click Import CSV.\par
- Select the converted CSV from Deb's PDF package.\par
- Map each incoming column to WileyWidget fields (for example Name -> AccountName, Number -> AccountNumber, Type -> FundType).\par
- Apply mapping to import your chart quickly and consistently.\par\par

{\b 3. What the Buttons Do}\par
- Load Budgets: refreshes budget data using current filters.\par
- Add/Edit/Delete: maintains individual budget lines.\par
- Import CSV: opens mapping workflow for chart import.\par
- Export CSV/PDF/Excel: prepares council-ready and audit-ready output.\par\par

{\b 4. How Payments Link to Budget}\par
PaymentsPanel writes transactions to municipal accounts, and MunicipalAccountId links those entries to budget lines.\par
That link is the source of truth for Actual values shown in this panel.\par\par

{\b 5. Filters and Summary}\par
Use fiscal year, entity, department, fund type, and variance filters to narrow focus.\par
Summary totals at the top always reflect the current filtered view.\par\par

{\b 6. Single Source of Truth}\par
Your chart of accounts defines reporting structure for imports, budgeting, and payment rollups.\par
Keeping account mapping accurate ensures reliable Actuals, Variances, and exports.\par\par

Need more guidance? Use the panel help button and JARVIS chat for step-by-step support.\par
}";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _walkthroughBox?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
