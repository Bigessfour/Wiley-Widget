using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services;

internal readonly record struct ExportExecutionResult(
    bool IsSuccess,
    bool IsCancelled,
    bool IsSkipped,
    string FilePath,
    string? ErrorMessage)
{
    public static ExportExecutionResult Success(string filePath) => new(true, false, false, filePath, null);

    public static ExportExecutionResult Cancelled() => new(false, true, false, string.Empty, null);

    public static ExportExecutionResult Skipped(string message) => new(false, false, true, string.Empty, message);

    public static ExportExecutionResult Failed(string message) => new(false, false, false, string.Empty, message);
}

internal static class ExportWorkflowService
{
    private static readonly ConcurrentDictionary<string, byte> ActiveOperations = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<ExportExecutionResult> ExecuteWithSaveDialogAsync(
        IWin32Window? owner,
        string operationKey,
        string dialogTitle,
        string filter,
        string defaultExtension,
        string defaultFileName,
        Func<string, CancellationToken, Task> exportAction,
        Action<string>? statusCallback = null,
        ILogger? logger = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            throw new ArgumentException("Export filter cannot be empty.", nameof(filter));
        }

        if (string.IsNullOrWhiteSpace(defaultFileName))
        {
            throw new ArgumentException("Default file name cannot be empty.", nameof(defaultFileName));
        }

        if (exportAction == null)
        {
            throw new ArgumentNullException(nameof(exportAction));
        }

        var key = string.IsNullOrWhiteSpace(operationKey) ? "ExportWorkflow.Default" : operationKey.Trim();
        if (!ActiveOperations.TryAdd(key, 0))
        {
            var inProgressMessage = "An export is already in progress.";
            statusCallback?.Invoke(inProgressMessage);
            logger?.LogDebug("Skipped export because operation key {OperationKey} is already active", key);
            return ExportExecutionResult.Skipped(inProgressMessage);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedExtension = NormalizeExtension(defaultExtension);
            using var saveDialog = new SaveFileDialog
            {
                Filter = filter,
                DefaultExt = normalizedExtension,
                AddExtension = true,
                CheckPathExists = true,
                OverwritePrompt = true,
                RestoreDirectory = true,
                ValidateNames = true,
                FileName = defaultFileName,
                Title = dialogTitle
            };

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                saveDialog.InitialDirectory = initialDirectory;
            }

            var dialogResult = owner != null ? saveDialog.ShowDialog(owner) : saveDialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                statusCallback?.Invoke("Export cancelled.");
                logger?.LogDebug("Export cancelled by user for operation key {OperationKey}", key);
                return ExportExecutionResult.Cancelled();
            }

            var selectedPath = NormalizePath(saveDialog.FileName, normalizedExtension);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                const string message = "Please choose a valid file path for export.";
                statusCallback?.Invoke(message);
                return ExportExecutionResult.Failed(message);
            }

            var directoryPath = Path.GetDirectoryName(selectedPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                const string message = "The selected export folder does not exist.";
                statusCallback?.Invoke(message);
                return ExportExecutionResult.Failed(message);
            }

            var ownerControl = owner as Control;
            try
            {
                if (ownerControl != null)
                {
                    ownerControl.UseWaitCursor = true;
                }

                statusCallback?.Invoke("Exporting...");
                cancellationToken.ThrowIfCancellationRequested();
                await exportAction(selectedPath, cancellationToken);
                logger?.LogInformation("Export completed successfully to {FilePath}", selectedPath);
                return ExportExecutionResult.Success(selectedPath);
            }
            finally
            {
                if (ownerControl != null)
                {
                    ownerControl.UseWaitCursor = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            statusCallback?.Invoke("Export cancelled.");
            logger?.LogInformation("Export operation was cancelled for key {OperationKey}", key);
            return ExportExecutionResult.Cancelled();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Export operation failed for key {OperationKey}", key);
            return ExportExecutionResult.Failed(ex.Message);
        }
        finally
        {
            ActiveOperations.TryRemove(key, out _);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "dat";
        }

        return extension.Trim().TrimStart('.');
    }

    private static string NormalizePath(string? path, string normalizedExtension)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (string.IsNullOrWhiteSpace(Path.GetExtension(trimmed)))
        {
            trimmed = Path.ChangeExtension(trimmed, normalizedExtension);
        }

        return trimmed;
    }
}
