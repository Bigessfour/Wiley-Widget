using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using WileyWidget.Services;

namespace WileyWidget.Startup.Tests
{
    public class MemoryCacheServiceTests
    {
        [Fact]
        public async Task SetGetRemoveClearAll_Works()
        {
            using var mem = new MemoryCache(new MemoryCacheOptions());
            var svc = new MemoryCacheService(mem, logger: null);

            await svc.SetAsync("k1", "v1");
            var v = await svc.GetAsync<string>("k1");
            Assert.Equal("v1", v);

            Assert.True(await svc.ExistsAsync("k1"));

            await svc.RemoveAsync("k1");
            Assert.False(await svc.ExistsAsync("k1"));

            await svc.SetAsync("k2", "v2");
            await svc.ClearAllAsync();
            var v2 = await svc.GetAsync<string>("k2");
            Assert.Null(v2);
        }
    }
}
