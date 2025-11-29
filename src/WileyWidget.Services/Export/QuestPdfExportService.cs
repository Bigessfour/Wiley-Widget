using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Abstractions;
using WileyWidget.Models;
using Polly;
using WileyWidget.Services.Startup;
using System;

namespace WileyWidget.Services.Export
{
    /// <summary>
    /// DEPRECATED: QuestPDF-based exporter stub.
    ///
    /// This file is retained for history but the implementation has been removed.
    /// Use `IPdfService` (SyncfusionPdfService) instead.
    /// Attempts to construct or call this type will throw <see cref="NotSupportedException"/>.
    /// </summary>
    [Obsolete("QuestPdfExportService is deprecated. Use IPdfService (SyncfusionPdfService) instead.")]
    public sealed class QuestPdfExportService
    {
        public QuestPdfExportService()
        {
            throw new NotSupportedException("QuestPdfExportService is deprecated and removed. Use IPdfService (SyncfusionPdfService) instead.");
        }
    }
}
