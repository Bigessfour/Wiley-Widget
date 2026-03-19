using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Intuit.Ipp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public QuickBooksDesktopImportService(
        ILogger<QuickBooksDesktopImportService> logger,
        IServiceProvider serviceProvider,
        QuickBooksDesktopIifParser iifParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _iifParser = iifParser ?? throw new ArgumentNullException(nameof(iifParser));
    }

    public async Task<ImportResult> ImportDesktopFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;

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

            var extension = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return Failure("QuickBooks Desktop import requires a supported file extension.", startedAt);
            }

            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            return extension.ToLowerInvariant() switch
            {
                ".csv" => await ImportCsvAsync(filePath, services, startedAt, cancellationToken).ConfigureAwait(false),
                ".iif" => await ImportIifAsync(filePath, services, startedAt, cancellationToken).ConfigureAwait(false),
                ".xls" or ".xlsx" => Failure(
                    "Excel QuickBooks Desktop imports are not available in this build. Export the file as CSV or IIF and import that export instead.",
                    startedAt,
                    importEntityType: "Excel"),
                _ => Failure(
                    $"Unsupported QuickBooks Desktop file type '{extension}'. Supported types are .csv, .iif, .xls, and .xlsx.",
                    startedAt)
            };
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
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var parsedFile = await ReadDelimitedFileAsync(filePath, ',', cancellationToken).ConfigureAwait(false);
        if (parsedFile.Rows.Count == 0)
        {
            return Failure("The CSV file does not contain any importable rows.", startedAt);
        }

        if (IsAccountExport(parsedFile.Headers))
        {
            return await ImportAccountsAsync(filePath, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsCustomerExport(parsedFile.Headers))
        {
            return await ImportCustomersAsync(filePath, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsVendorExport(parsedFile.Headers))
        {
            return await ImportVendorsAsync(filePath, parsedFile.Rows, services, startedAt, cancellationToken).ConfigureAwait(false);
        }

        if (IsItemExport(parsedFile.Headers))
        {
            return Failure(
                "QuickBooks Desktop item import target is not yet supported in Wiley Widget.",
                startedAt,
                importEntityType: "Items");
        }

        return Failure(
            "The CSV file does not match a supported QuickBooks Desktop export profile.",
            startedAt);
    }

    private async Task<ImportResult> ImportIifAsync(
        string filePath,
        IServiceProvider services,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var table = await _iifParser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (table.Rows.Count == 0)
        {
            return Failure("The IIF file does not contain any importable rows.", startedAt);
        }

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
                continue;
            }

            var transactionType = GetRowValue(row, "TRNSTYPE");
            if (!string.Equals(transactionType, "CHECK", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(transactionType, "PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            var checkNumber = GetRowValue(row, "DOCNUM");
            if (string.IsNullOrWhiteSpace(checkNumber))
            {
                skipped++;
                continue;
            }

            var existingPayments = await paymentRepository.GetByCheckNumberAsync(checkNumber, cancellationToken).ConfigureAwait(false);
            if (existingPayments.Count > 0)
            {
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

    private static async Task<ParsedDelimitedFile> ReadDelimitedFileAsync(string filePath, char delimiter, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (lines.Count == 0)
        {
            return new ParsedDelimitedFile(Array.Empty<string>(), Array.Empty<Dictionary<string, string>>());
        }

        var headers = ParseDelimitedLine(lines[0], delimiter)
            .Select(header => header.Trim())
            .ToArray();

        var rows = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Count; index++)
        {
            var values = ParseDelimitedLine(lines[index], delimiter);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < headers.Length; column++)
            {
                row[headers[column]] = column < values.Count ? values[column].Trim() : string.Empty;
            }

            rows.Add(row);
        }

        return new ParsedDelimitedFile(headers, rows);
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

    private sealed record ParsedDelimitedFile(
        IReadOnlyList<string> Headers,
        IReadOnlyList<Dictionary<string, string>> Rows);
}


