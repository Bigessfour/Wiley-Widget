using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Parses QuickBooks Desktop IIF files into a normalized data table.
/// </summary>
public sealed class QuickBooksDesktopIifParser
{
    private readonly ILogger<QuickBooksDesktopIifParser> _logger;

    public QuickBooksDesktopIifParser(ILogger<QuickBooksDesktopIifParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DataTable> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("IIF file not found.", filePath);
        }

        var table = new DataTable("QuickBooksDesktopIif")
        {
            Locale = System.Globalization.CultureInfo.InvariantCulture
        };
        table.Columns.Add("_RecordType", typeof(string));
        table.Columns.Add("_SourceLineNumber", typeof(int));

        var headerMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        var lineNumber = 0;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length == 0)
            {
                continue;
            }

            var token = cells[0].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.StartsWith('!'))
            {
                var recordType = token[1..];
                headerMap[recordType] = cells;

                for (var index = 1; index < cells.Length; index++)
                {
                    EnsureColumn(table, cells[index], index);
                }

                continue;
            }

            var row = table.NewRow();
            row["_RecordType"] = token;
            row["_SourceLineNumber"] = lineNumber;

            if (!headerMap.TryGetValue(token, out var headers))
            {
                headers = Array.Empty<string>();
            }

            for (var index = 1; index < cells.Length; index++)
            {
                var columnName = headers.Length > index
                    ? EnsureColumn(table, headers[index], index)
                    : EnsureColumn(table, $"Column{index}", index);

                row[columnName] = cells[index]?.Trim() ?? string.Empty;
            }

            table.Rows.Add(row);
        }

        _logger.LogInformation("Parsed {RowCount} data rows from QuickBooks Desktop IIF file {FilePath}", table.Rows.Count, filePath);
        return table;
    }

    private static string EnsureColumn(DataTable table, string rawName, int fallbackIndex)
    {
        var normalized = NormalizeColumnName(rawName, fallbackIndex);
        if (!table.Columns.Contains(normalized))
        {
            table.Columns.Add(normalized, typeof(string));
        }

        return normalized;
    }

    private static string NormalizeColumnName(string? rawName, int fallbackIndex)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return $"Column{fallbackIndex}";
        }

        var trimmed = rawName.Trim();
        var buffer = trimmed
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var normalized = new string(buffer).Trim('_');

        return string.IsNullOrWhiteSpace(normalized) ? $"Column{fallbackIndex}" : normalized;
    }
}
