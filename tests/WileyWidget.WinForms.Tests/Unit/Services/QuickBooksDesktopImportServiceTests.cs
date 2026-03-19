using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
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

    private static IQuickBooksDesktopImportService CreateService(ServiceProvider provider)
    {
        var parser = new QuickBooksDesktopIifParser(NullLogger<QuickBooksDesktopIifParser>.Instance);
        return new QuickBooksDesktopImportService(
            NullLogger<QuickBooksDesktopImportService>.Instance,
            provider,
            parser);
    }
}
