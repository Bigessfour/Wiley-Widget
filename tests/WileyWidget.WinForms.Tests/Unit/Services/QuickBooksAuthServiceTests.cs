using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksAuthServiceTests
{
    [Fact]
    public async Task RefreshTokenIfNeededAsync_ClearsCachedState_WhenRefreshTokenIsInvalid()
    {
        var settings = new TestSettingsService(new AppSettings
        {
            QboAccessToken = "expired-access-token",
            QboRefreshToken = "expired-refresh-token",
            QboTokenExpiry = DateTime.UtcNow.AddHours(-1),
            QuickBooksAccessToken = "legacy-access-token",
            QuickBooksRefreshToken = "legacy-refresh-token",
            QuickBooksTokenExpiresUtc = DateTime.UtcNow.AddHours(-1),
        });

        var secretVault = new TestSecretVaultService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["QBO-CLIENT-ID"] = "client-id",
            ["QBO-CLIENT-SECRET"] = "client-secret",
            ["QBO-REDIRECT-URI"] = "http://localhost:5000/callback/",
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var tokenStore = new QuickBooksTokenStore(
            loggerFactory.CreateLogger<QuickBooksTokenStore>(),
            Options.Create(new QuickBooksOAuthOptions
            {
                EnableTokenPersistence = false,
                RedirectUri = "http://localhost:5000/callback/",
            }),
            secretVault);

        await tokenStore.SaveTokenAsync(new QuickBooksOAuthToken
        {
            AccessToken = "cached-access-token",
            RefreshToken = "cached-refresh-token",
            TokenType = "Bearer",
            ExpiresIn = 3600,
            RefreshTokenExpiresIn = 86400,
            IssuedAtUtc = DateTime.UtcNow,
        });

        using var httpClient = new HttpClient(new StubHttpMessageHandler(static (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}"),
            })));

        var sut = new QuickBooksAuthService(
            settings,
            secretVault,
            loggerFactory.CreateLogger("QuickBooksAuthServiceTests"),
            httpClient,
            new ServiceCollection().BuildServiceProvider(),
            tokenStore,
            Options.Create(new QuickBooksOAuthOptions
            {
                RedirectUri = "http://localhost:5000/callback/",
                EnableTokenPersistence = false,
            }));

        var act = async () => await sut.RefreshTokenIfNeededAsync();

        await act.Should().ThrowAsync<QuickBooksAuthException>()
            .WithMessage("*QuickBooks authorization expired*");

        settings.Current.QboAccessToken.Should().BeNull();
        settings.Current.QboRefreshToken.Should().BeNull();
        settings.Current.QboTokenExpiry.Should().Be(default);
        settings.Current.QuickBooksAccessToken.Should().BeNull();
        settings.Current.QuickBooksRefreshToken.Should().BeNull();
        settings.Current.QuickBooksTokenExpiresUtc.Should().BeNull();
        settings.SaveCalls.Should().BeGreaterThan(0);
        (await tokenStore.GetTokenAsync()).Should().BeNull();
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public TestSettingsService(AppSettings current)
        {
            Current = current;
        }

        public int SaveCalls { get; private set; }

        public AppSettings Current { get; }

        public string Get(string key) => string.Empty;

        public void Set(string key, string value)
        {
        }

        public string GetEnvironmentName() => "Development";

        public string GetValue(string key) => string.Empty;

        public void SetValue(string key, string value)
        {
        }

        public void Save()
        {
            SaveCalls++;
        }

        public void SaveFiscalYearSettings(int month, int day)
        {
        }

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestSecretVaultService : ISecretVaultService
    {
        private readonly Dictionary<string, string> _values;

        public TestSecretVaultService(Dictionary<string, string> values)
        {
            _values = values;
        }

        public string? GetSecret(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public void StoreSecret(string key, string value)
        {
            _values[key] = value;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetSecret(key));
        }

        public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RotateSecretAsync(string secretName, string newValue, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[secretName] = newValue;
            return Task.CompletedTask;
        }

        public Task MigrateSecretsFromEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task PopulateProductionSecretsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<string> ExportSecretsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(string.Join(';', _values.Select(pair => $"{pair.Key}={pair.Value}")));
        }

        public Task ImportSecretsAsync(string jsonSecrets, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> ListSecretKeysAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IEnumerable<string>>(_values.Keys.ToArray());
        }

        public Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.Remove(secretName);
            return Task.CompletedTask;
        }

        public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult($"Secrets: {_values.Count}");
        }
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
    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshTokenIsInvalid_ClearsCachedAuthorizationState()
    {
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(service => service.Current).Returns(new AppSettings
        {
            QboAccessToken = "access-token",
            QboRefreshToken = "refresh-token",
            QboTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        });

        var secretVault = new Mock<ISecretVaultService>();
        secretVault
            .Setup(service => service.GetSecretAsync("QBO-CLIENT-ID", It.IsAny<CancellationToken>()))
            .ReturnsAsync("client-id");
        secretVault
            .Setup(service => service.GetSecretAsync("QBO-CLIENT-SECRET", It.IsAny<CancellationToken>()))
            .ReturnsAsync("client-secret");
        secretVault
            .Setup(service => service.GetSecretAsync("QBO-REDIRECT-URI", It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost:5000/callback/");
        secretVault
            .Setup(service => service.GetSecretAsync("QBO-REALM-ID", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        secretVault
            .Setup(service => service.GetSecretAsync("QBO-ENVIRONMENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = new QuickBooksAuthService(
            settings.Object,
            secretVault.Object,
            NullLogger.Instance,
            new HttpClient(new StubHttpMessageHandler(static (_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
                }))),
            Mock.Of<IServiceProvider>(),
            tokenStore: null,
            Options.Create(new QuickBooksOAuthOptions()));

        var result = await service.RefreshTokenAsync("refresh-token");

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().MatchRegex("(?i)quickbooks authorization");
        result.ErrorMessage.Should().MatchRegex("(?i)(re-?authorize|reconnect)");
        settings.Object.Current.QboAccessToken.Should().BeNull();
        settings.Object.Current.QboRefreshToken.Should().BeNull();
        settings.Object.Current.QboTokenExpiry.Should().Be(default);
        settings.Verify(current => current.Save(), Times.Once);
    }
}
