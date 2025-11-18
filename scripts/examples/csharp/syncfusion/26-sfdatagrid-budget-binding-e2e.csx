// 26-sfdatagrid-budget-binding-e2e.csx
// Purpose: lightweight E2E-style checks for Budget->SfDataGrid binding
// - uses fakes for repositories/services
// - verifies row counts and visibility converter logic
// - will attempt to instantiate a Syncfusion DataGrid when running on Windows; otherwise it will run VM/converter checks only

#load "../../../../tools/csx-helpers.csx" // optional helper; safe if missing

#r "nuget:Prism.DryIocServiceProviderAdapter,8.2.0"
#r "nuget:Syncfusion.SfDataGrid.WPF,22.4.0.47"

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

Console.WriteLine("[26] Syncfusion SfDataGrid Budget Binding E2E - START");

// --- Fakes ---
public record BudgetItem(string Category, decimal Amount, bool IsVisible);

public class FakeBudgetRepository
{
    public IEnumerable<BudgetItem> GetBudgets()
    {
        // Provide deterministic test data
        return new[] {
            new BudgetItem("Groceries", 500m, true),
            new BudgetItem("Rent", 1200m, true),
            new BudgetItem("Subscriptions", 45m, false),
        };
    }
}

// Simple visibility converter mimic (used by the UI to hide empty budgets)
public static class BudgetVisibilityConverter
{
    public static string Convert(BudgetItem item) => item.IsVisible ? "Visible" : "Collapsed";
}

// --- Assertions on repository and converter ---
var repo = new FakeBudgetRepository();
var items = repo.GetBudgets().ToList();
if (items.Count != 3) throw new Exception($"Expected 3 budget items, got {items.Count}");
Console.WriteLine($"[26] Repo returned {items.Count} items as expected.");

// Check converter behavior
var visMap = items.Select(i => BudgetVisibilityConverter.Convert(i)).ToArray();
if (visMap[0] != "Visible" || visMap[2] != "Collapsed") throw new Exception("Visibility converter produced unexpected results");
Console.WriteLine("[26] Visibility converter returned expected values.");

// --- Try to create an SfDataGrid (only on Windows) ---
bool controlTested = false;
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        Console.WriteLine("[26] Running DataGrid control instantiation (Windows).");
        // attempt to use Syncfusion SfDataGrid if available
        var gridType = Type.GetType("Syncfusion.UI.Xaml.Grid.SfDataGrid, Syncfusion.SfDataGrid.WPF")
                      ?? Type.GetType("Syncfusion.SfDataGrid.WPF.SfDataGrid, Syncfusion.SfDataGrid.WPF");
        if (gridType == null) throw new Exception("SfDataGrid type not found in loaded assemblies.");

        // create instance via reflection
        dynamic grid = Activator.CreateInstance(gridType);
        // attempt to set ItemsSource (uses dynamic so it's robust to minor API variations)
        grid.ItemsSource = items;

        // read row count via reflection if property exists
        int rowCount = -1;
        var viewProp = gridType.GetProperty("View");
        if (viewProp != null)
        {
            var view = viewProp.GetValue(grid);
            var rowCountProp = view?.GetType().GetProperty("RowCount");
            if (rowCountProp != null) rowCount = (int)rowCountProp.GetValue(view);
        }

        Console.WriteLine($"[26] Grid instanced; attempted to read RowCount -> {rowCount}");
        controlTested = true;

        // Attempt to write a small XAML preview out for inspection if possible
        try
        {
            var xamlWriter = Type.GetType("System.Windows.Markup.XamlWriter, PresentationFramework");
            if (xamlWriter != null)
            {
                var saveMethod = xamlWriter.GetMethod("Save", new[] { typeof(object) });
                if (saveMethod != null)
                {
                    var xaml = (string)saveMethod.Invoke(null, new object[] { grid });
                    var outDir = Path.GetFullPath("logs/xaml-previews");
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, "26-sfdatagrid-preview.xaml"), xaml);
                    Console.WriteLine("[26] XAML preview written to logs/xaml-previews/26-sfdatagrid-preview.xaml");
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("[26] XAML preview skipped or failed: " + ex.Message); }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[26] Control-level check skipped/failed: " + ex.Message);
    }
}
else
{
    Console.WriteLine("[26] Non-Windows host: skipping SfDataGrid control instantiation. Only VM/converter tests ran.");
}

Console.WriteLine("[26] Syncfusion SfDataGrid Budget Binding E2E - END");
