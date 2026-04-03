using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Imports QuickBooks Desktop local exports into Wiley Widget repositories.
/// Supports chart-of-accounts, customers, vendors, and payment/check exports.
/// </summary>
public sealed class QuickBooksDesktopImportService : IQuickBooksDesktopImportService
{
    private readonly ILogger<QuickBooksDesktopImportService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly QuickBooksDesktopIifParser _iifParser;
    private readonly IAppEventBus? _eventBus;

    public QuickBooksDesktopImportService(
        ILogger<QuickBooksDesktopImportService> logger,
        IServiceProvider serviceProvider,
        QuickBooksDesktopIifParser iifParser,
        IAppEventBus? eventBus = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _iifParser = iifParser ?? throw new ArgumentNullException(nameof(iifParser));
        _eventBus = eventBus;
    }

    public async Task<ImportResult> ImportDesktopFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var importId = BuildImportCorrelationId(filePath);

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Failure("A QuickBooks Desktop file path is required.", startedAt);
            }

            if (!File.Exists(filePath))
            {
                return Failure($"QuickBooks Desktop file not found: {filePath}", startedAt);
            }

            _logger.LogInformation(
                "QuickBooks Desktop import {ImportId} started for {FileName} ({FileType})",
                importId,
                Path.GetFileName(filePath),
                Path.GetExtension(filePath).ToLowerInvariant());

            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return Failure("QuickBooks Desktop import requires a supported file extension.", startedAt);
            }

            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var result = extension.ToLowerInvariant() switch
            {
                ".csv" => await ImportCsvAsync(filePath, importId, services, startedAt, cancellationToken).ConfigureAwait(false),
                ".iif" => await ImportIifAsync(filePath, importId, services, startedAt, cancellationToken).ConfigureAwait(false),
                ".xls" or ".xlsx" => await ImportExcelAsync(filePath, importId, services, startedAt, cancellationToken).ConfigureAwait(false),
                _ => Failure(
                    $"Unsupported QuickBooks Desktop file type '{extension}'. Supported types are .csv, .iif, .xls, and .xlsx.",
                    startedAt)
            };

            if (result.Success)
            {
                _logger.LogInformation(
                    "QuickBooks Desktop import {ImportId} completed for {FileName}: entity={ImportEntityType}, imported={Imported}, updated={Updated}, skipped={Skipped}",
                    importId,
                    Path.GetFileName(filePath),
                    result.ImportEntityType,
                    result.RecordsImported > 0 ? result.RecordsImported : result.AccountsImported,
                    result.RecordsUpdated > 0 ? result.RecordsUpdated : result.AccountsUpdated,
                    result.RecordsSkipped > 0 ? result.RecordsSkipped : result.AccountsSkipped);

                PublishImportCompletedEvent(filePath, result);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickBooks Desktop import failed for {FilePath}", filePath);
            return Failure(ex.Message, startedAt);
        }
    }

    private async Task<ImportResult> ImportCsvAsync(
        string filePath,
        string importId,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var rawRows = await ReadDelimitedRowsAsync(filePath, ',', cancellationToken).ConfigureAwait(false);
        var parsedFile = NormalizeTabularRows(rawRows);
        return await ImportStructuredTabularFileAsync(filePath, importId, parsedFile, services, startedAt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImportResult> ImportExcelAsync(
        string filePath,
        string importId,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var rawRows = await ReadExcelRowsAsync(filePath, cancellationToken).ConfigureAwait(false);
        var parsedFile = NormalizeTabularRows(rawRows);
        return await ImportStructuredTabularFileAsync(filePath, importId, parsedFile, services, startedAt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImportResult> ImportStructuredTabularFileAsync(
        string filePath,
        string importId,
        ParsedTabularFile parsedFile,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        if (parsedFile.Rows.Count == 0)
        {
            _logger.LogWarning(
                "QuickBooks Desktop import {ImportId} found no importable rows in {FileName}",
                importId,
                Path.GetFileName(filePath));

            return Failure("The file does not contain any importable rows.", startedAt);
        }

        _logger.LogDebug(
            "QuickBooks Desktop import {ImportId} normalized {RowCount} rows and {HeaderCount} headers for {FileName}",
            importId,
            parsedFile.Rows.Count,
            parsedFile.Headers.Count,
            Path.GetFileName(filePath));

        if (IsAccountExport(parsedFile.Headers))
        {
            _logger.LogInformation(
                "QuickBooks Desktop import {ImportId} classified {FileName} as Accounts export",
                importId,
                Path.GetFileName(filePath));

            return await ImportAccountsAsync(filePath, importId, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsCustomerExport(parsedFile.Headers))
        {
            _logger.LogInformation(
                "QuickBooks Desktop import {ImportId} classified {FileName} as Customers export",
                importId,
                Path.GetFileName(filePath));

            return await ImportCustomersAsync(filePath, importId, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsVendorExport(parsedFile.Headers))
        {
            _logger.LogInformation(
                "QuickBooks Desktop import {ImportId} classified {FileName} as Vendors export",
                importId,
                Path.GetFileName(filePath));

            return await ImportVendorsAsync(filePath, importId, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsTransactionExport(parsedFile.Headers))
        {
            _logger.LogInformation(
                "QuickBooks Desktop import {ImportId} classified {FileName} as Payments export",
                importId,
                Path.GetFileName(filePath));

            return await ImportPaymentsAsync(filePath, importId, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsItemExport(parsedFile.Headers))
        {
            _logger.LogWarning(
                "QuickBooks Desktop import {ImportId} classified {FileName} as unsupported Items export",
                importId,
                Path.GetFileName(filePath));

            return Failure(
                "QuickBooks Desktop item import target is not yet supported in Wiley Widget.",
                startedAt,
                importEntityType: "Items");
        }

        _logger.LogWarning(
            "QuickBooks Desktop import {ImportId} could not classify {FileName} from headers: {Headers}",
            importId,
            Path.GetFileName(filePath),
            string.Join("|", parsedFile.Headers));

        return Failure(
            $"The file '{Path.GetFileName(filePath)}' does not match a supported QuickBooks Desktop export profile.",
            startedAt);
    }

    private async Task<ImportResult> ImportIifAsync(
        string filePath,
        string importId,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var table = await _iifParser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (table.Rows.Count == 0)
        {
            _logger.LogWarning(
                "QuickBooks Desktop import {ImportId} found no importable rows in IIF file {FileName}",
                importId,
                Path.GetFileName(filePath));

            return Failure("The IIF file does not contain any importable rows.", startedAt);
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} classified {FileName} as IIF payments export with {RowCount} rows",
            importId,
            Path.GetFileName(filePath),
            table.Rows.Count);

        var paymentRepository = services.GetRequiredService<IPaymentRepository>();
        var vendorRepository = services.GetRequiredService<IVendorRepository>();
        var accountRepository = services.GetRequiredService<IMunicipalAccountRepository>();

        var imported = 0;
        var skipped = 0;

        foreach (DataRow row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(GetRowValue(row, "_RecordType"), "TRNS", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped IIF row because _RecordType was {RecordType}",
                    importId,
                    GetRowValue(row, "_RecordType"));

                continue;
            }

            var transactionType = GetRowValue(row, "TRNSTYPE");
            if (!string.Equals(transactionType, "CHECK", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(transactionType, "PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped IIF payment row because TRNSTYPE was {TransactionType}",
                    importId,
                    transactionType);

                skipped++;
                continue;
            }

            var checkNumber = GetRowValue(row, "DOCNUM");
            if (string.IsNullOrWhiteSpace(checkNumber))
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped IIF payment row because DOCNUM was blank",
                    importId);

                skipped++;
                continue;
            }

            var existingPayments = await paymentRepository.GetByCheckNumberAsync(checkNumber, cancellationToken).ConfigureAwait(false);
            if (existingPayments.Count > 0)
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped IIF payment row because check number {CheckNumber} already exists",
                    importId,
                    checkNumber);

                skipped++;
                continue;
            }

            var payee = GetRowValue(row, "NAME");
            var accountText = GetRowValue(row, "ACCNT");
            var accountNumber = ExtractLeadingAccountNumber(accountText);
            var vendor = string.IsNullOrWhiteSpace(payee)
                ? null
                : await vendorRepository.GetByNameAsync(payee, cancellationToken).ConfigureAwait(false);
            var account = string.IsNullOrWhiteSpace(accountNumber)
                ? null
                : await accountRepository.GetByAccountNumberAsync(accountNumber, cancellationToken).ConfigureAwait(false);

            var payment = new WileyWidget.Models.Payment
            {
                CheckNumber = checkNumber,
                PaymentDate = ParseDate(GetRowValue(row, "DATE")) ?? DateTime.Today,
                Payee = payee,
                Amount = Math.Abs(ParseDecimal(GetRowValue(row, "AMOUNT"))),
                Description = FirstNonEmpty(GetRowValue(row, "MEMO"), transactionType, "QuickBooks Desktop import"),
                MunicipalAccountId = account?.Id,
                VendorId = vendor?.Id,
                Memo = GetRowValue(row, "MEMO"),
                Status = "Pending"
            };

            await paymentRepository.AddAsync(payment, cancellationToken).ConfigureAwait(false);
            imported++;
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} completed IIF payments import for {FileName}: imported={Imported}, skipped={Skipped}",
            importId,
            Path.GetFileName(filePath),
            imported,
            skipped);

        await AuditAsync(services, filePath, "Payments", imported, skipped, cancellationToken).ConfigureAwait(false);

        return new ImportResult
        {
            Success = true,
            ImportEntityType = "Payments",
            RecordsImported = imported,
            RecordsSkipped = skipped,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private async Task<ImportResult> ImportAccountsAsync(
        string filePath,
        string importId,
        IReadOnlyList<Dictionary<string, string>> rows,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IMunicipalAccountRepository>();
        var accounts = new List<Account>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            accounts.Add(new Account
            {
                AcctNum = GetValue(row, "AccountNumber"),
                Name = GetValue(row, "Name"),
                AccountType = ParseQuickBooksAccountType(GetValue(row, "AccountType")),
                CurrentBalance = ParseDecimal(GetValue(row, "Balance")),
                Active = true
            });
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} completed Accounts import for {FileName}: imported={Imported}",
            importId,
            Path.GetFileName(filePath),
            accounts.Count);

        await repository.ImportChartOfAccountsAsync(accounts, cancellationToken).ConfigureAwait(false);
        await AuditAsync(services, filePath, "Accounts", accounts.Count, 0, cancellationToken).ConfigureAwait(false);

        return new ImportResult
        {
            Success = true,
            ImportEntityType = "Accounts",
            RecordsImported = accounts.Count,
            AccountsImported = accounts.Count,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private async Task<ImportResult> ImportCustomersAsync(
        string filePath,
        string importId,
        IReadOnlyList<Dictionary<string, string>> rows,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IUtilityCustomerRepository>();
        var imported = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accountNumber = GetValue(row, "AccountNumber");
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped customer row because AccountNumber was blank",
                    importId);

                continue;
            }

            var existing = await repository.GetByAccountNumberAsync(accountNumber, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                var customer = new UtilityCustomer
                {
                    AccountNumber = accountNumber,
                    FirstName = GetValue(row, "FirstName"),
                    LastName = GetValue(row, "LastName"),
                    ServiceAddress = GetValue(row, "ServiceAddress"),
                    ServiceCity = GetValue(row, "ServiceCity"),
                    ServiceState = GetValue(row, "ServiceState").ToUpperInvariant(),
                    ServiceZipCode = GetValue(row, "ServiceZipCode"),
                    EmailAddress = NullIfWhiteSpace(GetValue(row, "Email")),
                    PhoneNumber = NullIfWhiteSpace(GetValue(row, "Phone"))
                };

                await repository.AddAsync(customer, cancellationToken).ConfigureAwait(false);
                imported++;
                continue;
            }

            existing.FirstName = GetValue(row, "FirstName");
            existing.LastName = GetValue(row, "LastName");
            existing.ServiceAddress = GetValue(row, "ServiceAddress");
            existing.ServiceCity = GetValue(row, "ServiceCity");
            existing.ServiceState = GetValue(row, "ServiceState").ToUpperInvariant();
            existing.ServiceZipCode = GetValue(row, "ServiceZipCode");
            existing.EmailAddress = NullIfWhiteSpace(GetValue(row, "Email"));
            existing.PhoneNumber = NullIfWhiteSpace(GetValue(row, "Phone"));

            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            updated++;
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} completed Customers import for {FileName}: imported={Imported}, updated={Updated}",
            importId,
            Path.GetFileName(filePath),
            imported,
            updated);

        await AuditAsync(services, filePath, "Customers", imported, 0, cancellationToken).ConfigureAwait(false);

        return new ImportResult
        {
            Success = true,
            ImportEntityType = "Customers",
            RecordsImported = imported,
            RecordsUpdated = updated,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private async Task<ImportResult> ImportVendorsAsync(
        string filePath,
        string importId,
        IReadOnlyList<Dictionary<string, string>> rows,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IVendorRepository>();
        var imported = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = GetValue(row, "VendorName");
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogDebug(
                    "QuickBooks Desktop import {ImportId} skipped vendor row because VendorName was blank",
                    importId);

                continue;
            }

            var existing = await repository.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                var vendor = new WileyWidget.Models.Vendor
                {
                    Name = name,
                    Email = NullIfWhiteSpace(GetValue(row, "Email")),
                    Phone = NullIfWhiteSpace(GetValue(row, "Phone")),
                    MailingAddressLine1 = NullIfWhiteSpace(GetValue(row, "Address1")),
                    MailingAddressCity = NullIfWhiteSpace(GetValue(row, "City")),
                    MailingAddressState = NullIfWhiteSpace(GetValue(row, "State")),
                    MailingAddressPostalCode = NullIfWhiteSpace(GetValue(row, "Zip"))
                };

                await repository.AddAsync(vendor, cancellationToken).ConfigureAwait(false);
                imported++;
                continue;
            }

            existing.Email = NullIfWhiteSpace(GetValue(row, "Email"));
            existing.Phone = NullIfWhiteSpace(GetValue(row, "Phone"));
            existing.MailingAddressLine1 = NullIfWhiteSpace(GetValue(row, "Address1"));
            existing.MailingAddressCity = NullIfWhiteSpace(GetValue(row, "City"));
            existing.MailingAddressState = NullIfWhiteSpace(GetValue(row, "State"));
            existing.MailingAddressPostalCode = NullIfWhiteSpace(GetValue(row, "Zip"));

            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            updated++;
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} completed Vendors import for {FileName}: imported={Imported}, updated={Updated}",
            importId,
            Path.GetFileName(filePath),
            imported,
            updated);

        await AuditAsync(services, filePath, "Vendors", imported, 0, cancellationToken).ConfigureAwait(false);

        return new ImportResult
        {
            Success = true,
            ImportEntityType = "Vendors",
            RecordsImported = imported,
            RecordsUpdated = updated,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private async Task<ImportResult> ImportPaymentsAsync(
        string filePath,
        string importId,
        IReadOnlyList<Dictionary<string, string>> rows,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var paymentRepository = services.GetRequiredService<IPaymentRepository>();
        var vendorRepository = services.GetRequiredService<IVendorRepository>();
        var accountRepository = services.GetRequiredService<IMunicipalAccountRepository>();

        var imported = 0;
        var skipped = 0;
        var usedCheckNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            cancellationToken.ThrowIfCancellationRequested();

            var transactionType = FirstNonEmpty(GetValue(row, "Type"), GetValue(row, "TRNSTYPE"), "QuickBooks Desktop import");
            var checkNumber = await ResolvePaymentCheckNumberAsync(
                filePath,
                importId,
                rowIndex,
                FirstNonEmpty(GetValue(row, "Num"), GetValue(row, "DOCNUM"), GetValue(row, "CheckNumber")),
                usedCheckNumbers,
                paymentRepository,
                cancellationToken).ConfigureAwait(false);

            if (checkNumber == null)
            {
                _logger.LogWarning(
                    "QuickBooks Desktop import {ImportId} skipped payment row {RowNumber} in {FileName} because no unique check number could be resolved",
                    importId,
                    rowIndex + 1,
                    Path.GetFileName(filePath));

                skipped++;
                continue;
            }

            _logger.LogDebug(
                "QuickBooks Desktop import {ImportId} accepted payment row {RowNumber} in {FileName}: transactionType={TransactionType}, checkNumber={CheckNumber}, payee={Payee}",
                importId,
                rowIndex + 1,
                Path.GetFileName(filePath),
                transactionType,
                checkNumber,
                FirstNonEmpty(GetValue(row, "Name"), GetValue(row, "Payee"), GetValue(row, "Memo"), transactionType, "QuickBooks Desktop import"));

            var payee = FirstNonEmpty(GetValue(row, "Name"), GetValue(row, "Payee"), GetValue(row, "Memo"), transactionType, "QuickBooks Desktop import");

            var accountText = FirstNonEmpty(GetValue(row, "Split"), GetValue(row, "Account"), GetValue(row, "ACCNT"), GetValue(row, "Memo"));
            var accountNumber = ExtractLeadingAccountNumber(accountText);
            var vendor = string.IsNullOrWhiteSpace(payee)
                ? null
                : await vendorRepository.GetByNameAsync(payee, cancellationToken).ConfigureAwait(false);
            var account = string.IsNullOrWhiteSpace(accountNumber)
                ? null
                : await accountRepository.GetByAccountNumberAsync(accountNumber, cancellationToken).ConfigureAwait(false);

            var payment = new WileyWidget.Models.Payment
            {
                CheckNumber = checkNumber,
                PaymentDate = ParseDate(GetValue(row, "Date")) ?? DateTime.Today,
                Payee = payee,
                Amount = Math.Abs(ParseDecimal(GetValue(row, "Amount"))),
                Description = FirstNonEmpty(GetValue(row, "Memo"), transactionType, accountText, "QuickBooks Desktop import"),
                MunicipalAccountId = account?.Id,
                VendorId = vendor?.Id,
                Memo = GetValue(row, "Memo"),
                Status = "Pending"
            };

            await paymentRepository.AddAsync(payment, cancellationToken).ConfigureAwait(false);
            imported++;
        }

        _logger.LogInformation(
            "QuickBooks Desktop import {ImportId} completed Payments import for {FileName}: imported={Imported}, skipped={Skipped}",
            importId,
            Path.GetFileName(filePath),
            imported,
            skipped);

        await AuditAsync(services, filePath, "Payments", imported, skipped, cancellationToken).ConfigureAwait(false);

        return new ImportResult
        {
            Success = true,
            ImportEntityType = "Payments",
            RecordsImported = imported,
            RecordsSkipped = skipped,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<IReadOnlyList<string>>> ReadDelimitedRowsAsync(string filePath, char delimiter, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var rows = new List<IReadOnlyList<string>>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                rows.Add(ParseDelimitedLine(line, delimiter));
            }
        }

        return rows;
    }

    private static System.Threading.Tasks.Task<IReadOnlyList<IReadOnlyList<string>>> ReadExcelRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.Run(() =>
        {
            var rows = new List<IReadOnlyList<string>>();

            using var excelEngine = new ExcelEngine();
            var application = excelEngine.Excel;
            application.DefaultVersion = Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase)
                ? ExcelVersion.Excel97to2003
                : ExcelVersion.Xlsx;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var workbook = application.Workbooks.Open(stream);

            try
            {
                var worksheet = workbook.Worksheets[0];
                var usedRange = worksheet.UsedRange;
                if (usedRange == null)
                {
                    return (IReadOnlyList<IReadOnlyList<string>>)rows;
                }

                for (var rowIndex = usedRange.Row; rowIndex <= usedRange.LastRow; rowIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var values = new List<string>(usedRange.LastColumn - usedRange.Column + 1);
                    for (var columnIndex = usedRange.Column; columnIndex <= usedRange.LastColumn; columnIndex++)
                    {
                        values.Add(NormalizeCellValue(worksheet.Range[rowIndex, columnIndex].DisplayText));
                    }

                    rows.Add(values);
                }

                return (IReadOnlyList<IReadOnlyList<string>>)rows;
            }
            finally
            {
                workbook.Close();
            }
        }, cancellationToken);
    }

    private static ParsedTabularFile NormalizeTabularRows(IReadOnlyList<IReadOnlyList<string>> rawRows)
    {
        if (rawRows.Count == 0)
        {
            return new ParsedTabularFile(Array.Empty<string>(), Array.Empty<Dictionary<string, string>>());
        }

        var headerRowIndex = FindHeaderRowIndex(rawRows);
        if (headerRowIndex < 0)
        {
            return new ParsedTabularFile(Array.Empty<string>(), Array.Empty<Dictionary<string, string>>());
        }

        var headers = rawRows[headerRowIndex]
            .Select(NormalizeCellValue)
            .ToArray();

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = headerRowIndex + 1; rowIndex < rawRows.Count; rowIndex++)
        {
            var sourceRow = rawRows[rowIndex];
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasAnyValue = false;

            for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
            {
                var header = headers[columnIndex];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var value = columnIndex < sourceRow.Count ? NormalizeCellValue(sourceRow[columnIndex]) : string.Empty;
                row[header] = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasAnyValue = true;
                }
            }

            if (hasAnyValue)
            {
                rows.Add(row);
            }
        }

        return new ParsedTabularFile(headers, rows);
    }

    private static int FindHeaderRowIndex(IReadOnlyList<IReadOnlyList<string>> rawRows)
    {
        for (var index = 0; index < Math.Min(rawRows.Count, 12); index++)
        {
            if (IsSupportedHeaderRow(rawRows[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsSupportedHeaderRow(IReadOnlyList<string> row)
    {
        var headerSet = row
            .Select(NormalizeCellValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return headerSet.Contains("AccountNumber")
            || headerSet.Contains("VendorName")
            || headerSet.Contains("FirstName")
            || headerSet.Contains("TRNSTYPE")
            || (headerSet.Contains("Type") && headerSet.Contains("Date") && headerSet.Contains("Num"))
            || (headerSet.Contains("AccountType") && headerSet.Contains("Balance"))
            || (headerSet.Contains("ItemName") && headerSet.Contains("ItemType"));
    }

    private static bool IsTransactionExport(IEnumerable<string> headers)
    {
        var headerSet = ToHeaderSet(headers);
        return headerSet.Contains("Type")
            && headerSet.Contains("Date")
            && headerSet.Contains("Num")
            && headerSet.Contains("Name")
            && headerSet.Contains("Amount")
            && (headerSet.Contains("Split") || headerSet.Contains("Account") || headerSet.Contains("ACCNT"));
    }

    private static string NormalizeCellValue(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values;
    }

    private static bool IsAccountExport(IEnumerable<string> headers)
    {
        var headerSet = ToHeaderSet(headers);
        return headerSet.Contains("AccountNumber")
            && headerSet.Contains("Name")
            && headerSet.Contains("AccountType");
    }

    private static bool IsCustomerExport(IEnumerable<string> headers)
    {
        var headerSet = ToHeaderSet(headers);
        return headerSet.Contains("AccountNumber")
            && headerSet.Contains("FirstName")
            && headerSet.Contains("LastName");
    }

    private static bool IsVendorExport(IEnumerable<string> headers)
    {
        var headerSet = ToHeaderSet(headers);
        return headerSet.Contains("VendorName");
    }

    private static bool IsItemExport(IEnumerable<string> headers)
    {
        var headerSet = ToHeaderSet(headers);
        return headerSet.Contains("ItemName") && headerSet.Contains("ItemType");
    }

    private static HashSet<string> ToHeaderSet(IEnumerable<string> headers)
    {
        return headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => header.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetValue(IReadOnlyDictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static string GetRowValue(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName)
            ? Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static decimal ParseDecimal(string rawValue)
    {
        return decimal.TryParse(rawValue, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static DateTime? ParseDate(string rawValue)
    {
        return DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;
    }

    private static AccountTypeEnum ParseQuickBooksAccountType(string rawValue)
    {
        return Enum.TryParse<AccountTypeEnum>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : default;
    }

    private static string ExtractLeadingAccountNumber(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var token = rawValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return token ?? string.Empty;
    }

    private async Task<string?> ResolvePaymentCheckNumberAsync(
        string filePath,
        string importId,
        int rowIndex,
        string sourceCheckNumber,
        ISet<string> usedCheckNumbers,
        IPaymentRepository paymentRepository,
        CancellationToken cancellationToken)
    {
        var normalizedSourceCheckNumber = NormalizeCheckNumber(sourceCheckNumber);
        if (!string.IsNullOrWhiteSpace(normalizedSourceCheckNumber)
            && normalizedSourceCheckNumber.Length <= 20
            && !usedCheckNumbers.Contains(normalizedSourceCheckNumber)
            && !await paymentRepository.CheckNumberExistsAsync(normalizedSourceCheckNumber, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            usedCheckNumbers.Add(normalizedSourceCheckNumber);
            _logger.LogDebug(
                "QuickBooks Desktop import {ImportId} row {RowNumber} in {FileName} resolved source check number {CheckNumber}",
                importId,
                rowIndex + 1,
                Path.GetFileName(filePath),
                normalizedSourceCheckNumber);
            return normalizedSourceCheckNumber;
        }

        var syntheticCheckNumber = BuildSyntheticCheckNumber(filePath, rowIndex);
        if (usedCheckNumbers.Contains(syntheticCheckNumber)
            || await paymentRepository.CheckNumberExistsAsync(syntheticCheckNumber, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning(
                "QuickBooks Desktop import {ImportId} row {RowNumber} in {FileName} could not resolve a unique check number (source={SourceCheckNumber}, synthetic={SyntheticCheckNumber})",
                importId,
                rowIndex + 1,
                Path.GetFileName(filePath),
                string.IsNullOrWhiteSpace(normalizedSourceCheckNumber) ? "<blank>" : normalizedSourceCheckNumber,
                syntheticCheckNumber);

            return null;
        }

        usedCheckNumbers.Add(syntheticCheckNumber);
        _logger.LogDebug(
            "QuickBooks Desktop import {ImportId} row {RowNumber} in {FileName} assigned synthetic check number {SyntheticCheckNumber} from source {SourceCheckNumber}",
            importId,
            rowIndex + 1,
            Path.GetFileName(filePath),
            syntheticCheckNumber,
            string.IsNullOrWhiteSpace(normalizedSourceCheckNumber) ? "<blank>" : normalizedSourceCheckNumber);
        return syntheticCheckNumber;
    }

    private static string NormalizeCheckNumber(string? checkNumber)
    {
        var value = checkNumber?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= 20)
        {
            return value;
        }

        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
        return $"{value[..15]}-{hash[..4]}";
    }

    private static string BuildSyntheticCheckNumber(string filePath, int rowIndex)
    {
        var fileToken = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(filePath)))).Substring(0, 8);
        return $"QB-{fileToken}-{rowIndex + 1:000000}";
    }

    private static string BuildImportCorrelationId(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var token = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fileName))).Substring(0, 8);
        return $"QBIMP-{token}";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private void PublishImportCompletedEvent(string filePath, ImportResult result)
    {
        if (_eventBus == null)
        {
            return;
        }

        var recordsImported = result.RecordsImported > 0 ? result.RecordsImported : result.AccountsImported;
        var recordsUpdated = result.RecordsUpdated > 0 ? result.RecordsUpdated : result.AccountsUpdated;
        var recordsSkipped = result.RecordsSkipped > 0 ? result.RecordsSkipped : result.AccountsSkipped;

        try
        {
            _eventBus.Publish(new QuickBooksDesktopImportCompletedEvent(
                filePath,
                result.ImportEntityType,
                recordsImported,
                recordsUpdated,
                recordsSkipped,
                result.Duration));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish QuickBooksDesktopImportCompletedEvent for {FilePath}", filePath);
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ImportResult Failure(string message, DateTime startedAt, string? importEntityType = null)
    {
        return new ImportResult
        {
            Success = false,
            ImportEntityType = importEntityType,
            ErrorMessage = message,
            Duration = DateTime.UtcNow - startedAt
        };
    }

    private static async System.Threading.Tasks.Task AuditAsync(
        IServiceProvider services,
        string filePath,
        string entityType,
        int imported,
        int skipped,
        CancellationToken cancellationToken)
    {
        var auditService = services.GetService<IAuditService>();
        if (auditService == null)
        {
            return;
        }

        await auditService.AuditAsync(
            "QuickBooksDesktopImport",
            new
            {
                Source = "QuickBooks Desktop",
                FilePath = filePath,
                FileType = Path.GetExtension(filePath),
                ImportEntityType = entityType,
                RecordsImported = imported,
                RecordsSkipped = skipped
            },
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record ParsedTabularFile(
        IReadOnlyList<string> Headers,
        IReadOnlyList<Dictionary<string, string>> Rows);
}


