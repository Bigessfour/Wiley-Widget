using System;
using Syncfusion.WinForms.Controls;
using Xunit;

namespace WileyWidget.WinForms.Tests.Infrastructure;

/// <summary>
/// Collection fixture to ensure Syncfusion theme system is initialized early on STA thread
/// to prevent enumeration corruption during parallel test execution.
/// </summary>
[CollectionDefinition("SyncfusionTheme", DisableParallelization = true)]
public class SyncfusionThemeCollection : ICollectionFixture<SyncfusionThemeFixture>
{
}

public sealed class SyncfusionThemeFixture : IDisposable
{
    public SyncfusionThemeFixture()
    {
        // Initialize Syncfusion theme globals on the current thread (safe — no Win32 handles created here).
        // We intentionally do NOT create WinForms controls in a warmup thread without a message pump:
        // RibbonControlAdv / DockingManager call SendMessage internally during CreateControl /
        // PerformLayout / Refresh, which blocks permanently on a thread with no pump and hangs the
        // test host, causing the xunit runner to time out and exit with no TRX output.
        try
        {

            // Load theme assembly and set the global theme — these are registry/static writes
            // that do NOT create window handles and are safe without a message pump.
            Syncfusion.Windows.Forms.SkinManager.LoadAssembly(
                typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly);
            SfSkinManager.LoadAssembly(
                typeof(Syncfusion.WinForms.Themes.Office2019Theme).Assembly);

            if (string.IsNullOrWhiteSpace(SfSkinManager.ApplicationVisualTheme))
            {
                SfSkinManager.ApplicationVisualTheme = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
            }
        }
        catch (Exception ex)
        {
            // Best-effort — theme globals are nice-to-have; individual tests set their own theme.
            Console.WriteLine($"[SyncfusionThemeFixture] Theme init warning: {ex.Message}");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
