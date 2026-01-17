using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;  // Added for JSON
using System.Threading.Tasks;
using Microsoft.SemanticKernel; // Added for KernelFunction


namespace WileyWidget.Services.Plugins
{
    /// <summary>
    /// Semantic Kernel plugin for detecting anomalies in financial data.
    /// Provides statistical analysis tools that can be invoked by AI agents.
    /// </summary>
    public class AnomalyDetectionPlugin
    {
        [KernelFunction]
        [Description("Performs statistical analysis on a set of numbers to find outliers using Z-Score.")]
        [return: Description("A text summary of detected outliers with their z-scores.")]
        public string PerformStatisticalAnalysis(
            [Description("Comma separated list of values (e.g. '10.5, 20.1, 500.0')")] string valuesCsv)
        {
            if (string.IsNullOrWhiteSpace(valuesCsv))
                return "No data provided.";

            var values = valuesCsv.Split(',')
                                  .Select(v => v.Trim())
                                  .Where(v => double.TryParse(v, out _))
                                  .Select(double.Parse)
                                  .ToList();
            
            if (values.Count == 0) return "No valid numeric data found.";

            var mean = values.Average();
            var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
            
            var sb = new StringBuilder();
            sb.AppendLine($"Analysis Result:");
            sb.AppendLine($"Count: {values.Count}, Mean: {mean:F2}, StdDev: {stdDev:F2}");
            
            bool outlierFound = false; // Initialize to false
            for (int i = 0; i < values.Count; i++)
            {
                var zScore = stdDev > 0 ? (values[i] - mean) / stdDev : 0;
                // Threshold of 2.0 for outlier detection
                if (Math.Abs(zScore) > 2.0)
                {
                    sb.AppendLine($"- Value {values[i]} is an outlier (Z-Score: {zScore:F2})");
                    outlierFound = true;
                }
            }

            if (!outlierFound) // Only print if false
            {
                sb.AppendLine("No statistical outliers detected.");
            }
            
            return sb.ToString();
        }
    }
}