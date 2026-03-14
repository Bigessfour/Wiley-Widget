using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksAccountServiceTests
{
    [Fact]
    public async Task GetAccountsByClassificationAsync_FiltersAccountsCaseInsensitively()
    {
        var service = await CreateServiceAsync(BuildAccountsResponseJson());

        var accounts = await service.GetAccountsByClassificationAsync("asset");

        accounts.Should().HaveCount(1);
        accounts[0].AccountId.Should().Be("1");
        accounts[0].Name.Should().Be("Cash");
    }

    [Fact]
    public async Task GetAccountBalanceAsync_ReturnsBalanceForMatchingAccount()
    {
        var service = await CreateServiceAsync(BuildAccountsResponseJson());

        var balance = await service.GetAccountBalanceAsync("2");

        balance.Should().Be(275.55m);
    }

    private static async Task<QuickBooksAccountService> CreateServiceAsync(string jsonResponse)
    {
        var authService = new Mock<IQuickBooksAuthService>();
        authService
            .Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuickBooksOAuthToken
            {
                AccessToken = "access-token",
                ExpiresIn = 3600,
                IssuedAtUtc = DateTime.UtcNow,
            });
        authService
            .Setup(service => service.GetEnvironment())
            .Returns("sandbox");

        var tokenStore = new QuickBooksTokenStore(
            NullLogger<QuickBooksTokenStore>.Instance,
            Options.Create(new QuickBooksOAuthOptions
            {
                EnableTokenPersistence = false,
                RedirectUri = "http://localhost:5000/callback/",
            }));

        await tokenStore.SetRealmIdAsync("sandbox-realm");

        var httpClient = new HttpClient(new StubHttpMessageHandler((_, cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse),
            })));

        return new QuickBooksAccountService(
            httpClient,
            authService.Object,
            NullLogger<QuickBooksAccountService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            tokenStore);
    }

    private static string BuildAccountsResponseJson()
    {
        return """
        {
          "QueryResponse": {
            "Account": [
              {
                "Id": "1",
                "Name": "Cash",
                "AccountType": "Bank",
                "Classification": "Asset",
                "AccountSubType": "Checking",
                "CurrentBalance": 1200.50,
                "SyncToken": "0",
                "Active": true,
                "CurrencyRef": { "value": "USD" }
              },
              {
                "Id": "2",
                "Name": "Utilities Expense",
                "AccountType": "Expense",
                "Classification": "Expense",
                "AccountSubType": "Utilities",
                "CurrentBalance": 275.55,
                "SyncToken": "1",
                "Active": true,
                "CurrencyRef": { "value": "USD" }
              }
            ]
          }
        }
        """;
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
