using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Syncfusion.XlsIO;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksDesktopImportServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public QuickBooksDesktopImportServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WileyWidgetDesktopImportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithChartOfAccountsCsv_ImportsAccounts()
    {
        var filePath = Path.Combine(_tempDirectory, "chart-of-accounts.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "AccountNumber,Name,AccountType,Balance",
                "1000,Cash,Asset,1250.50",
                "2000,Accounts Payable,Liability,410.75"));

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.ImportChartOfAccountsAsync(
                It.Is<List<Intuit.Ipp.Data.Account>>(accounts => accounts.Count == 2),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Accounts");
        result.RecordsImported.Should().Be(2);
        result.AccountsImported.Should().Be(2);
        accountRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithChartOfAccountsCsv_PublishesCompletionEvent()
    {
        var filePath = Path.Combine(_tempDirectory, "chart-of-accounts.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "AccountNumber,Name,AccountType,Balance",
                "1000,Cash,Asset,1250.50"));

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.ImportChartOfAccountsAsync(
                It.Is<List<Intuit.Ipp.Data.Account>>(accounts => accounts.Count == 1),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var auditService = CreateAuditMock();
        var eventBus = new Mock<IAppEventBus>(MockBehavior.Strict);
        eventBus
            .Setup(bus => bus.Publish(It.Is<QuickBooksDesktopImportCompletedEvent>(evt =>
                evt.FilePath == filePath
                && evt.ImportEntityType == "Accounts"
                && evt.RecordsImported == 1
                && evt.RecordsUpdated == 0
                && evt.RecordsSkipped == 0)))
            .Verifiable();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider, eventBus: eventBus.Object);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        eventBus.Verify();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithCustomerCsv_ImportsCustomers()
    {
        var filePath = Path.Combine(_tempDirectory, "customers.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "AccountNumber,FirstName,LastName,ServiceAddress,ServiceCity,ServiceState,ServiceZipCode,Email,Phone",
                "CUST-001,Ada,Lovelace,123 Main St,Wiley,CO,81092,ada@example.com,555-1000"));

        var customerRepository = new Mock<IUtilityCustomerRepository>(MockBehavior.Strict);
        customerRepository
            .Setup(repository => repository.GetByAccountNumberAsync("CUST-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UtilityCustomer?)null);
        customerRepository
            .Setup(repository => repository.AddAsync(
                It.Is<UtilityCustomer>(customer =>
                    customer.AccountNumber == "CUST-001"
                    && customer.FirstName == "Ada"
                    && customer.LastName == "Lovelace"
                    && customer.ServiceState == "CO"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UtilityCustomer customer, CancellationToken _) => customer);

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => customerRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Customers");
        result.RecordsImported.Should().Be(1);
        result.RecordsUpdated.Should().Be(0);
        customerRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithVendorCsv_ImportsVendors()
    {
        var filePath = Path.Combine(_tempDirectory, "vendors.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "VendorName,Email,Phone,Address1,City,State,Zip",
                "Acme Supplies,acme@example.com,555-2000,45 Industrial Rd,Wiley,CO,81092"));

        var vendorRepository = new Mock<IVendorRepository>(MockBehavior.Strict);
        vendorRepository
            .Setup(repository => repository.GetByNameAsync("Acme Supplies", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);
        vendorRepository
            .Setup(repository => repository.AddAsync(
                It.Is<Vendor>(vendor => vendor.Name == "Acme Supplies" && vendor.MailingAddressState == "CO"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor vendor, CancellationToken _) => vendor);

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => vendorRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Vendors");
        result.RecordsImported.Should().Be(1);
        vendorRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithPaymentIif_ImportsPayments()
    {
        var filePath = Path.Combine(_tempDirectory, "check-register.iif");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "!TRNS\tTRNSTYPE\tDATE\tACCNT\tNAME\tDOCNUM\tAMOUNT\tMEMO",
                "TRNS\tCHECK\t3/3/2010\t1000 Operating\tArchCo Gas\t10023\t-70\tFuel bill"));

        var paymentRepository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        paymentRepository
            .Setup(repository => repository.GetByCheckNumberAsync("10023", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
        paymentRepository
            .Setup(repository => repository.AddAsync(
                It.Is<Payment>(payment =>
                    payment.CheckNumber == "10023"
                    && payment.Payee == "ArchCo Gas"
                    && payment.Amount == 70m
                    && payment.VendorId == 7
                    && payment.MunicipalAccountId == 12),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var vendorRepository = new Mock<IVendorRepository>(MockBehavior.Strict);
        vendorRepository
            .Setup(repository => repository.GetByNameAsync("ArchCo Gas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Vendor { Id = 7, Name = "ArchCo Gas" });

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetByAccountNumberAsync("1000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MunicipalAccount { Id = 12 });

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => paymentRepository.Object);
            services.AddScoped(_ => vendorRepository.Object);
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Payments");
        result.RecordsImported.Should().Be(1);
        paymentRepository.VerifyAll();
        vendorRepository.VerifyAll();
        accountRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithPaymentExcel_ImportsPayments()
    {
        var filePath = Path.Combine(_tempDirectory, "check-register.xlsx");
        await CreateExcelWorkbookAsync(
            filePath,
            new[]
            {
                new[] { "Type", "Date", "Num", "Name", "Memo", "Split", "Amount", "Balance" },
                new[] { "Check", "01/05/2026", "5108", "BELLOMY INSURANCE AGENCY", "Insurance premium", "430 · INSURANCE", "-2149.25", "108509.82" }
            });

        var paymentRepository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        paymentRepository
            .Setup(repository => repository.CheckNumberExistsAsync("5108", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        paymentRepository
            .Setup(repository => repository.AddAsync(
                It.Is<Payment>(payment =>
                    payment.CheckNumber == "5108"
                    && payment.Payee == "BELLOMY INSURANCE AGENCY"
                    && payment.Amount == 2149.25m
                    && payment.VendorId == 7
                    && payment.MunicipalAccountId == 12),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var vendorRepository = new Mock<IVendorRepository>(MockBehavior.Strict);
        vendorRepository
            .Setup(repository => repository.GetByNameAsync("BELLOMY INSURANCE AGENCY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Vendor { Id = 7, Name = "BELLOMY INSURANCE AGENCY" });

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetByAccountNumberAsync("430", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MunicipalAccount { Id = 12 });

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => paymentRepository.Object);
            services.AddScoped(_ => vendorRepository.Object);
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Payments");
        result.RecordsImported.Should().Be(1);
        paymentRepository.VerifyAll();
        vendorRepository.VerifyAll();
        accountRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithMixedPaymentRows_ImportsAllDataRows()
    {
        var filePath = Path.Combine(_tempDirectory, "mixed-transaction-register.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "Type,Date,Num,Name,Memo,Split,Amount,Balance",
                "Deposit,01/02/2026,,,Deposit,105 CASH IN BANK - UTILITY,362.90,362.90",
                "Check,01/02/2026,12122,RUPP'S TRUCK AND TRAILER,,453 TRASH SUPPLIES/REPAIRS,-125.80,-125.80",
                "General Journal,01/05/2026,363,,WALKER WELL LOAN USDA RD DCFO,211.3 WALKER WELL LOAN (USDA RD),-10256.00,-10256.00"));

        var capturedPayments = new List<Payment>();

        var paymentRepository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        paymentRepository
            .Setup(repository => repository.CheckNumberExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        paymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) =>
            {
                capturedPayments.Add(payment);
                return payment;
            });

        var vendorRepository = new Mock<IVendorRepository>(MockBehavior.Loose);
        vendorRepository
            .Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Loose);
        accountRepository
            .Setup(repository => repository.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MunicipalAccount?)null);

        var auditService = CreateAuditMock();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => paymentRepository.Object);
            services.AddScoped(_ => vendorRepository.Object);
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        result.ImportEntityType.Should().Be("Payments");
        result.RecordsImported.Should().Be(3);
        result.RecordsSkipped.Should().Be(0);
        capturedPayments.Should().HaveCount(3);
        capturedPayments.Select(payment => payment.CheckNumber).Should().OnlyHaveUniqueItems();
        capturedPayments.Should().Contain(payment => payment.Payee == "Deposit");
        capturedPayments.Should().Contain(payment => payment.Payee == "RUPP'S TRUCK AND TRAILER");
        capturedPayments.Should().Contain(payment => payment.Payee == "WALKER WELL LOAN USDA RD DCFO");
        paymentRepository.VerifyAll();
        auditService.VerifyAll();
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithMixedPaymentRows_EmitsDeterministicDiagnostics()
    {
        var filePath = Path.Combine(_tempDirectory, "mixed-transaction-register.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "Type,Date,Num,Name,Memo,Split,Amount,Balance",
                "Deposit,01/02/2026,,,Deposit,105 CASH IN BANK - UTILITY,362.90,362.90",
                "Check,01/02/2026,12122,RUPP'S TRUCK AND TRAILER,,453 TRASH SUPPLIES/REPAIRS,-125.80,-125.80",
                "General Journal,01/05/2026,363,,WALKER WELL LOAN USDA RD DCFO,211.3 WALKER WELL LOAN (USDA RD),-10256.00,-10256.00"));

        var paymentRepository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        paymentRepository
            .Setup(repository => repository.CheckNumberExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        paymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var vendorRepository = new Mock<IVendorRepository>(MockBehavior.Loose);
        vendorRepository
            .Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vendor?)null);

        var accountRepository = new Mock<IMunicipalAccountRepository>(MockBehavior.Loose);
        accountRepository
            .Setup(repository => repository.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MunicipalAccount?)null);

        var auditService = CreateAuditMock();
        var logger = new Mock<ILogger<QuickBooksDesktopImportService>>();

        await using var provider = BuildProvider(services =>
        {
            services.AddScoped(_ => paymentRepository.Object);
            services.AddScoped(_ => vendorRepository.Object);
            services.AddScoped(_ => accountRepository.Object);
            services.AddScoped(_ => auditService.Object);
        });

        var service = CreateService(provider, logger: logger.Object);
        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeTrue();
        VerifyLogContains(logger, LogLevel.Information, "started for mixed-transaction-register.csv");
        VerifyLogContains(logger, LogLevel.Information, "classified mixed-transaction-register.csv as Payments export");
        VerifyLogContains(logger, LogLevel.Debug, "row 1 in mixed-transaction-register.csv assigned synthetic check number");
        VerifyLogContains(logger, LogLevel.Debug, "accepted payment row 2 in mixed-transaction-register.csv");
        VerifyLogContains(logger, LogLevel.Information, "completed Payments import for mixed-transaction-register.csv: imported=3, skipped=0");
    }

    [Fact]
    public async Task ImportDesktopFileAsync_WithItemCsv_ReturnsExplicitUnsupportedFailure()
    {
        var filePath = Path.Combine(_tempDirectory, "items.csv");
        await File.WriteAllTextAsync(
            filePath,
            string.Join(Environment.NewLine,
                "ItemName,ItemType,Description of the Item",
                "Widget A,Inventory Part,Sample inventory item"));

        await using var provider = BuildProvider(_ => { });
        var service = CreateService(provider);

        var result = await service.ImportDesktopFileAsync(filePath);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("item import target");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static Mock<IAuditService> CreateAuditMock()
    {
        var auditService = new Mock<IAuditService>(MockBehavior.Strict);
        auditService
            .Setup(service => service.AuditAsync(
                "QuickBooksDesktopImport",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return auditService;
    }

    private static ServiceProvider BuildProvider(Action<ServiceCollection> registerServices)
    {
        var services = new ServiceCollection();
        registerServices(services);
        return services.BuildServiceProvider();
    }

    private static async Task CreateExcelWorkbookAsync(string filePath, IReadOnlyList<string[]> rows)
    {
        await Task.Run(() =>
        {
            using var excelEngine = new ExcelEngine();
            var application = excelEngine.Excel;
            application.DefaultVersion = ExcelVersion.Xlsx;
            var workbook = application.Workbooks.Create(1);
            var worksheet = workbook.Worksheets[0];

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    worksheet.Range[rowIndex + 1, columnIndex + 1].Text = row[columnIndex];
                }
            }

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.SaveAs(stream);
            workbook.Close();
        });
    }

    private static IQuickBooksDesktopImportService CreateService(
        ServiceProvider provider,
        ILogger<QuickBooksDesktopImportService>? logger = null,
        IAppEventBus? eventBus = null)
    {
        var parser = new QuickBooksDesktopIifParser(NullLogger<QuickBooksDesktopIifParser>.Instance);
        return new QuickBooksDesktopImportService(
            logger ?? NullLogger<QuickBooksDesktopImportService>.Instance,
            provider,
            parser,
            eventBus);
    }

    private static void VerifyLogContains(
        Mock<ILogger<QuickBooksDesktopImportService>> logger,
        LogLevel level,
        string expectedMessage,
        Times? times = null)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(expectedMessage, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times ?? Times.AtLeastOnce());
    }
}
