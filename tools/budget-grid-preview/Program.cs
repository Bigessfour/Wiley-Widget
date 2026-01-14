using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BudgetGridPreview
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using var form = new PreviewForm();

            // If run with --capture, auto-capture and exit; otherwise keep the window open for inspection.
            var args = Environment.GetCommandLineArgs();
            var captureMode = args != null && args.Any(a => string.Equals(a, "--capture", StringComparison.OrdinalIgnoreCase));

            if (captureMode)
            {
                form.Shown += async (s, e) =>
                {
                    // Give control time to layout and paint
                    await Task.Delay(1500);

                    try
                    {
                        var outPath = System.IO.Path.Combine(AppContext.BaseDirectory, "preview.png");
                        form.CaptureAndSave(outPath);
                    }
                    catch
                    {
                        // Ignore capture errors for preview run
                    }

                    // Close the form and exit
                    form.BeginInvoke(new Action(() => form.Close()));
                };
            }
            else
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.KeyPreview = true;
                form.KeyDown += (s, e) => {
                    if (e.KeyCode == Keys.Escape) form.Close();
                };
            }

            Application.Run(form);
        }
    }
}
