using System.Threading.Tasks;
using FluentAssertions;
using WileyWidget.Services;
using WileyWidget.Configuration;
using Xunit;

namespace WileyWidget.Tests.Services;

public class ApiKeyFacadeTests
{
    private class TestApiKeyService : ApiKeyService
    {
        public bool Stored; public bool Removed; public string LastTested;
        public override string ToString() => "TestApiKeyService"; // just to avoid warnings
        public new Task<bool> TestApiKeyAsync(string apiKey = null)
        { LastTested = apiKey; return Task.FromResult(apiKey == "valid-key"); }
        public new bool StoreApiKey(string apiKey, StorageMethod method = StorageMethod.Auto){ Stored = true; return !string.IsNullOrWhiteSpace(apiKey); }
        public new bool RemoveApiKey(){ Removed = true; return true; }
        public new ApiKeyInfo GetApiKeyInfo() => new ApiKeyInfo{ IsValid = Stored && !Removed };
    }

    [Fact]
    public void SaveSecure_SetsMarkerAndPersists()
    {
        var settings = SettingsService.Instance; settings.ResetForTests();
        var svc = new TestApiKeyService();
        var facade = new ApiKeyFacade(svc, settings);
        var ok = facade.SaveSecure("abc123XYZ");
        ok.Should().BeTrue();
        settings.Current.XaiApiKey.Should().Be("[SECURELY_STORED]");
    }

    [Fact]
    public async Task TestAsync_ValidatesKey()
    {
        var settings = SettingsService.Instance; settings.ResetForTests();
        var svc = new TestApiKeyService();
        var facade = new ApiKeyFacade(svc, settings);
        (await facade.TestAsync("valid-key")).Should().BeTrue();
        (await facade.TestAsync("nope" )).Should().BeFalse();
    }

    [Fact]
    public void Remove_ClearsSettings()
    {
        var settings = SettingsService.Instance; settings.ResetForTests();
        settings.Current.XaiApiKey = "[SECURELY_STORED]";
        var svc = new TestApiKeyService();
        var facade = new ApiKeyFacade(svc, settings);
        facade.Remove().Should().BeTrue();
        settings.Current.XaiApiKey.Should().BeNull();
    }
}
