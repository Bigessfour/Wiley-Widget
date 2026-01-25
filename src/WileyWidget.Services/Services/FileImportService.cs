using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Abstractions;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Implementation of IFileImportService for asynchronous file import with JSON/XML parsing support.
    /// Supports importing data (CSV, XLSX, XLS) and configuration (JSON, XML) files.
    /// Returns Result<T> for error handling without throwing exceptions.
    /// </summary>
    public class FileImportService : IFileImportService
    {
        private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB limit

        private readonly ILogger<FileImportService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileImportService(ILogger<FileImportService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure JSON deserialization options for flexibility
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Asynchronously validates that an import file exists, is accessible, and is not too large.
        /// Does NOT parse file contents; use ImportDataAsync for full validation.
        /// </summary>
        public async Task<Result> ValidateImportFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var errorMsg = "File path is null or empty";
                _logger.LogWarning(errorMsg);
                return Result.Failure(errorMsg);
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    var errorMsg = $"File not found: {filePath}";
                    _logger.LogWarning(errorMsg);
                    return Result.Failure(errorMsg);
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    var errorMsg = $"File is empty: {Path.GetFileName(filePath)}";
                    _logger.LogWarning(errorMsg);
                    return Result.Failure(errorMsg);
                }

                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    var errorMsg = $"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024}MB: {Path.GetFileName(filePath)} ({fileInfo.Length / 1024 / 1024}MB)";
                    _logger.LogWarning(errorMsg);
                    return Result.Failure(errorMsg);
                }

                _logger.LogDebug("File validation successful: {FilePath} ({Size} bytes)", Path.GetFileName(filePath), fileInfo.Length);
                return Result.Success();
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied reading file: {Path.GetFileName(filePath)}";
                _logger.LogWarning(ex, errorMsg);
                return Result.Failure(errorMsg);
            }
            catch (IOException ex)
            {
                var errorMsg = $"I/O error reading file: {Path.GetFileName(filePath)}";
                _logger.LogWarning(ex, errorMsg);
                return Result.Failure(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error validating file: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                return Result.Failure(errorMsg);
            }
        }

        /// <summary>
        /// Asynchronously imports and parses a file of the specified type.
        /// Supports JSON and XML formats. Returns a Result<T> with error details on failure.
        /// </summary>
        public async Task<Result<T>> ImportDataAsync<T>(string filePath, CancellationToken cancellationToken = default)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Result<T>.Failure("File path is null or empty");
            }

            try
            {
                // Validate file first
                var validationResult = await ValidateImportFileAsync(filePath, cancellationToken);
                if (!validationResult.IsSuccess)
                {
                    return Result<T>.Failure(validationResult.ErrorMessage ?? "File validation failed");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Read file content asynchronously
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                _logger.LogDebug("Read {Length} characters from {FilePath}", content.Length, Path.GetFileName(filePath));

                cancellationToken.ThrowIfCancellationRequested();

                // Determine format and parse accordingly
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                T? data = null;

                if (ext == ".json")
                {
                    try
                    {
                        data = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                        _logger.LogInformation("Successfully parsed JSON file: {FilePath}", Path.GetFileName(filePath));
                    }
                    catch (JsonException ex)
                    {
                        return Result<T>.Failure($"Invalid JSON format: {ex.Message}");
                    }
                }
                else if (ext == ".xml")
                {
                    // XML support: Try basic deserialization (requires DataContractSerializer setup)
                    try
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
                        using (var reader = new System.IO.StringReader(content))
                        {
                            data = serializer.Deserialize(reader) as T;
                        }
                        _logger.LogInformation("Successfully parsed XML file: {FilePath}", Path.GetFileName(filePath));
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Result<T>.Failure($"Invalid XML format: {ex.Message}");
                    }
                }
                else if (ext == ".csv" || ext == ".xlsx" || ext == ".xls")
                {
                    // These formats would require additional libraries (CsvHelper, EPPlus, etc.)
                    // For now, return informational result indicating format not yet supported
                    _logger.LogInformation("CSV/Excel import requested but not yet implemented for type {Type}", typeof(T).Name);
                    return Result<T>.Failure($"Import format {ext} is not yet supported for type {typeof(T).Name}");
                }
                else
                {
                    return Result<T>.Failure($"Unsupported file format: {ext}. Supported: .json, .xml");
                }

                if (data == null)
                {
                    return Result<T>.Failure("File was parsed but resulted in null data");
                }

                _logger.LogInformation("File import successful: {FilePath} -> {Type}", Path.GetFileName(filePath), typeof(T).Name);
                return Result<T>.Success(data);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("File import cancelled: {FilePath}", Path.GetFileName(filePath));
                return Result<T>.Failure("Import operation was cancelled");
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied reading file: {Path.GetFileName(filePath)}";
                _logger.LogWarning(ex, errorMsg);
                return Result<T>.Failure(errorMsg);
            }
            catch (IOException ex)
            {
                var errorMsg = $"I/O error reading file: {Path.GetFileName(filePath)}";
                _logger.LogWarning(ex, errorMsg);
                return Result<T>.Failure(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error importing file: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                return Result<T>.Failure(errorMsg);
            }
        }
    }
}
