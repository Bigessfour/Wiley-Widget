using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Services;

public static class ExportServiceCollectionExtensions
{
    /// <summary>
    /// Registers document export pipeline (PDF + Excel) using Syncfusion libraries.
    /// </summary>
    public static IServiceCollection AddDocumentExport(this IServiceCollection services)
    {
        // Register as singleton: stateless, short-lived object creation inside methods
        services.AddSingleton<IExportPipeline, SyncfusionExportPipeline>();
        return services;
    }
}
