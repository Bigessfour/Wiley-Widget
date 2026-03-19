using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Imports QuickBooks Desktop local export files into Wiley Widget.
/// </summary>
public interface IQuickBooksDesktopImportService
{
    /// <summary>
    /// Imports a QuickBooks Desktop local export file.
    /// </summary>
    /// <param name="filePath">The local export file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import result.</returns>
    Task<ImportResult> ImportDesktopFileAsync(string filePath, CancellationToken cancellationToken = default);
}
