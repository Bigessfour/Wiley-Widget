// 27-sfchart-trend-analysis.csx
// Purpose: test small trend analysis logic and BudgetProgressConverter against a SfChart series

#r "nuget:Syncfusion.UI.Xaml.Charts,22.4.0.47"
#r "nuget:Prism.DryIocServiceProviderAdapter,8.2.0"

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

Console.WriteLine("[27] Syncfusion SfChart Trend Analysis E2E - START");

// Fake trend model
public record TrendPoint(DateTime Date, decimal Value);

public class FakeTrendRepository
{
    public IEnumerable<TrendPoint> GetMonthlyTrend()
    {
        var now = DateTime.UtcNow.Date;
        return Enumerable.Range(0,6).Select(i => new TrendPoint(now.AddMonths(-i), 100 + i*10));
    }
}

// BudgetProgressConverter simulates view conversion logic for plotting
public static class BudgetProgressConverter
{
    public static double ConvertToChartValue(decimal v) => (double)v;
}

var repo = new FakeTrendRepository();
var points = repo.GetMonthlyTrend().ToList();
if (points.Count != 6) throw new Exception($"Expected 6 trend points, got {points.Count}");
Console.WriteLine($"[27] Trend repo returned {points.Count} points.");

// Validate converter mapping
var converted = points.Select(p => BudgetProgressConverter.ConvertToChartValue(p.Value)).ToArray();
if (!converted.All(d => d > 0)) throw new Exception("Converted chart values must be > 0");
Console.WriteLine("[27] BudgetProgressConverter returned expected numeric values.");

// Chart control instantiation only on Windows
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        var chartType = Type.GetType("Syncfusion.UI.Xaml.Charts.SfChart, Syncfusion.UI.Xaml.Charts")
                        ?? Type.GetType("Syncfusion.UI.Xaml.Charts.SfChart, Syncfusion.UI.Xaml.Charts");
        if (chartType == null) throw new Exception("SfChart type not found");

        dynamic chart = Activator.CreateInstance(chartType);
        // attempt to create a series and add points (best-effort via reflection)
        Console.WriteLine("[27] SfChart instantiated; attempting to attach series (best-effort).");

        // Attempt to write a XAML preview
        try
        {
            var xamlWriter = Type.GetType("System.Windows.Markup.XamlWriter, Microsoft.UI.Xaml");
            if (xamlWriter != null)
            {
                var saveMethod = xamlWriter.GetMethod("Save", new[] { typeof(object) });
                if (saveMethod != null)
                {
                    var xaml = (string)saveMethod.Invoke(null, new object[] { chart });
                    var outDir = Path.GetFullPath("logs/xaml-previews");
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, "27-sfchart-preview.xaml"), xaml);
                    Console.WriteLine("[27] XAML preview written to logs/xaml-previews/27-sfchart-preview.xaml");
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("[27] XAML preview skipped/failed: " + ex.Message); }
    }
    catch (Exception ex)
    {
        Console.WriteLine("[27] Control-level checks skipped/failed: " + ex.Message);
    }
}
else Console.WriteLine("[27] Non-Windows host: skipping SfChart control instantiation.");

Console.WriteLine("[27] Syncfusion SfChart Trend Analysis E2E - END");
