using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions;

namespace WileyWidget.WinForms.Services.Abstractions
{
    /// <summary>
    /// Provides abstraction for asynchronous file imports with JSON support.
    /// Handles file reads, basic validation, and deserialization.
    /// </summary>
    public interface IFileImportService
    {
        /// <summary>
        /// Imports data from a file and deserializes it to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the file content to.</typeparam>
        /// <param name="filePath">Path to the file to import.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Result containing the deserialized data or an error message.</returns>
        Task<Result<T>> ImportDataAsync<T>(string filePath, CancellationToken ct = default) where T : class;

        /// <summary>
        /// Validates that an import file exists and is readable.
        /// </summary>
        /// <param name="filePath">Path to the file to validate.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Result indicating whether the file is valid for import.</returns>
        Task<Result> ValidateImportFileAsync(string filePath, CancellationToken ct = default);
    }
}
