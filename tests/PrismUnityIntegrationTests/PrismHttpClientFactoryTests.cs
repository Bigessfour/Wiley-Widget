using System;
using System.Net.Http;
using Xunit;
using WileyWidget.Services;

public class PrismHttpClientFactoryTests
{
    [Fact]
    public void CreateClient_DefaultName_ReturnsHttpClient()
    {
    using var factory = new PrismHttpClientFactory(name => new HttpClient { Timeout = TimeSpan.FromSeconds(5) });
    var client = factory.CreateClient("");
    Assert.NotNull(client);
    Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
    }

    [Fact]
    public void CreateClient_DisposePreventsUsage()
    {
    var factory = new PrismHttpClientFactory(name => new HttpClient());
    factory.Dispose();
    Assert.Throws<ObjectDisposedException>(() => factory.CreateClient("Test"));
    }

    [Fact]
    public void CreateClient_NamedClients_AreIndependent()
    {
    using var factory = new PrismHttpClientFactory(name => new HttpClient { Timeout = name == "AIServices" ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(3) });
    var c1 = factory.CreateClient("AIServices");
    var c2 = factory.CreateClient("Other");
    Assert.Equal(TimeSpan.FromSeconds(10), c1.Timeout);
    Assert.Equal(TimeSpan.FromSeconds(3), c2.Timeout);
    }
}