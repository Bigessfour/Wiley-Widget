using System;
using System.Threading;
using System.Windows.Forms;
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
    private readonly Thread _initThread;

    public SyncfusionThemeFixture()
    {
        // Initialize theme system on STA thread to avoid race conditions
        _initThread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Set global theme before creating any controls
                Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

                // Create a dummy DockingManager to force theme initialization
                var dummyDocking = new Syncfusion.Windows.Forms.Tools.DockingManager();
                dummyDocking.ThemeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                dummyDocking.Dispose();

                // Create a dummy SfDataGrid to force DataGrid theme initialization
                var dummyGrid = new Syncfusion.WinForms.DataGrid.SfDataGrid();
                dummyGrid.ThemeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                dummyGrid.Dispose();

                // Create a dummy SfListView to force ListView theme initialization
                var dummyListView = new Syncfusion.WinForms.ListView.SfListView();
                dummyListView.ThemeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                dummyListView.Dispose();
            }
            catch (Exception ex)
            {
                // Log but don't fail - theme init is best-effort
                Console.WriteLine($"Theme fixture initialization warning: {ex.Message}");
            }
        });

        _initThread.SetApartmentState(ApartmentState.STA);
        _initThread.Start();
        _initThread.Join(TimeSpan.FromSeconds(10)); // Wait up to 10s for init
    }

    public void Dispose()
    {
        try
        {
            if (_initThread.IsAlive)
            {
                _initThread.Join(TimeSpan.FromSeconds(2));
            }
        }
        catch
        {
            // Ignore dispose errors
        }
        GC.SuppressFinalize(this);
    }
}
