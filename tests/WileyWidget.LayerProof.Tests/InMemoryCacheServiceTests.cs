using FluentAssertions;
using WileyWidget.Abstractions;
using WileyWidget.Services;

namespace WileyWidget.LayerProof.Tests;

public sealed class InMemoryCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryValue()
    {
        var cache = new InMemoryCacheService();
        var factoryCalls = 0;

        var first = await cache.GetOrCreateAsync("dashboard", () =>
        {
            factoryCalls++;
            return Task.FromResult<CachePayload>(new("first"));
        });

        var second = await cache.GetOrCreateAsync("dashboard", () =>
        {
            factoryCalls++;
            return Task.FromResult<CachePayload>(new("second"));
        });

        first.Value.Should().Be("first");
        second.Value.Should().Be("first");
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullAfterExpiration()
    {
        var cache = new InMemoryCacheService();
        await cache.SetAsync("token", new CachePayload("cached"), TimeSpan.FromMilliseconds(40));

        await Task.Delay(120);
        var value = await cache.GetAsync<CachePayload>("token");

        value.Should().BeNull();
    }

    private sealed record CachePayload(string Value);
}