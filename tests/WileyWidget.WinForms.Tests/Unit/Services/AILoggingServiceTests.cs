using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class AILoggingServiceTests
{
    [Fact]
    public async Task BeginCorrelationScope_AppliesCorrelationIdToQueryToolAndResponseEntries()
    {
        var service = new AILoggingService(NullLogger<AILoggingService>.Instance);
        const string correlationId = "jarvis-correlation-test";
        var windowStart = DateTime.UtcNow.AddMinutes(-1);

        using (service.BeginCorrelationScope(correlationId))
        {
            service.LogQuery("What time is it?", "JARVIS Chat", "grok-4-1-fast-reasoning");
            service.LogToolExecution("What time is it?", "SemanticKernel", new[] { "TimePlugin.GetCurrentLocalTime" });
            service.LogResponse("What time is it?", "The current local time is 11:30 AM.", 25, 12);
        }

        var entries = GetLogEntries(service)
            .Where(entry => string.Equals(GetPropertyValue(entry, "CorrelationId"), correlationId, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, entry => string.Equals(GetPropertyValue(entry, "EntryType"), "Query", StringComparison.Ordinal));
        Assert.Contains(entries, entry => string.Equals(GetPropertyValue(entry, "EntryType"), "ToolCall", StringComparison.Ordinal));
        Assert.Contains(entries, entry => string.Equals(GetPropertyValue(entry, "EntryType"), "Response", StringComparison.Ordinal));

        var exportPath = Path.GetTempFileName();
        try
        {
            await service.ExportLogsAsync(exportPath, windowStart, DateTime.UtcNow.AddMinutes(1));
            var exportedJson = await File.ReadAllTextAsync(exportPath);

            Assert.Contains("\"correlationId\": \"jarvis-correlation-test\"", exportedJson, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(exportPath);
        }
    }

    private static List<object> GetLogEntries(AILoggingService service)
    {
        var field = typeof(AILoggingService).GetField("_logEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var entries = field!.GetValue(service) as IEnumerable;
        Assert.NotNull(entries);

        return entries!.Cast<object>().ToList();
    }

    private static string? GetPropertyValue(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!.GetValue(target)?.ToString();
    }
}
