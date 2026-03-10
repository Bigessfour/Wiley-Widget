using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IntuitCustomer = Intuit.Ipp.Data.Customer;
using IntuitVendor = Intuit.Ipp.Data.Vendor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksServiceDataAccessTests
{
    [Fact]
    public async Task GetCustomersAsync_UsesInjectedDataService()
    {
        var dataService = new Mock<IQuickBooksDataService>();
        dataService
            .Setup(service => service.FindCustomers(1, 100))
            .Returns(new List<IntuitCustomer> { new(), new() });

        using var service = CreateQuickBooksService(dataService.Object);

        var customers = await service.GetCustomersAsync();

        customers.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetVendorsAsync_UsesInjectedDataService()
    {
        var dataService = new Mock<IQuickBooksDataService>();
        dataService
            .Setup(service => service.FindVendors(1, 100))
            .Returns(new List<IntuitVendor> { new(), new(), new() });

        using var service = CreateQuickBooksService(dataService.Object);

        var vendors = await service.GetVendorsAsync();

        vendors.Should().HaveCount(3);
    }

    [Fact]
    public void BuildInvoiceEnterpriseQuery_UsesEnterpriseCustomFieldFilter()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "BuildInvoiceEnterpriseQuery",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var query = method!.Invoke(null, new object[] { "Water" }) as string;

        query.Should().Be("SELECT * FROM Invoice WHERE Metadata.CustomField['Enterprise'] = 'Water'");
    }

    private static QuickBooksService CreateQuickBooksService(IQuickBooksDataService dataService)
    {
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(service => service.Current).Returns(new AppSettings());
        settings.Setup(service => service.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var secretVault = new TestSecretVaultService();
        var authService = new QuickBooksAuthService(
            settings.Object,
            secretVault,
            NullLogger.Instance,
            new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            new ServiceCollection().BuildServiceProvider(),
            tokenStore: null,
            Options.Create(new QuickBooksOAuthOptions
            {
                RedirectUri = "http://localhost:5000/callback/",
                EnableTokenPersistence = false,
            }));

        var services = new ServiceCollection();
        services.AddSingleton(authService);
        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        return new QuickBooksService(
            settings.Object,
            secretVault,
            NullLogger<QuickBooksService>.Instance,
            Mock.Of<IQuickBooksApiClient>(),
            httpClientFactory.Object,
            serviceProvider,
            dataService,
            Options.Create(new QuickBooksOAuthOptions
            {
                RedirectUri = "http://localhost:5000/callback/",
                EnableTokenPersistence = false,
            }));
    }

    private sealed class TestSecretVaultService : ISecretVaultService
    {
        public string? GetSecret(string key) => null;

        public void StoreSecret(string key, string value)
        {
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RotateSecretAsync(string secretName, string newValue, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MigrateSecretsFromEnvironmentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PopulateProductionSecretsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<string> ExportSecretsAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

        public Task ImportSecretsAsync(string jsonSecrets, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IEnumerable<string>> ListSecretKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

        public Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
