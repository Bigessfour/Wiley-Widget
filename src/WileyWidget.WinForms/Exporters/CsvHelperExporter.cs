using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using WileyWidget.Abstractions.Models;

namespace WileyWidget.WinForms.Exporters
{
    /// <summary>
    /// Exports accounts to CSV using CsvHelper in an async-friendly way.
    /// </summary>
    public static class CsvHelperExporter
    {
        /// <summary>
        /// Export accounts to CSV asynchronously. Reports progress as integer percent (0-100).
        /// </summary>
        public static async Task ExportToCsvAsync(IEnumerable<MunicipalAccountDisplay> accounts, Stream outputStream, CancellationToken cancellationToken, IProgress<int>? progress = null)
        {
            if (accounts == null) throw new ArgumentNullException(nameof(accounts));
            if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));

            // Ensure we can write
            using var writer = new StreamWriter(outputStream, System.Text.Encoding.UTF8, 4096, leaveOpen: true);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                NewLine = Environment.NewLine,
            });

            var list = accounts is IList<MunicipalAccountDisplay> l ? l : accounts.ToList();
            int total = list.Count;
            int processed = 0;

            // Write header using property names
            csv.WriteHeader<MunicipalAccountDisplay>();
            await csv.NextRecordAsync().ConfigureAwait(false);

            foreach (var item in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                csv.WriteRecord(item);
                await csv.NextRecordAsync().ConfigureAwait(false);

                processed++;
                if (progress != null)
                {
                    // report percent every time processed changes by at least 1% or on last item
                    int percent = total == 0 ? 100 : (int)((processed * 100L) / total);
                    progress.Report(percent);
                }
            }

            await writer.FlushAsync().ConfigureAwait(false);
            progress?.Report(100);
        }
    }
}
