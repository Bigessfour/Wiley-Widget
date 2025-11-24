using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;

namespace WileyWidget.IntegrationTests;

public class ServiceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ServiceIntegrationTests()
    {
        // Load test configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton(_configuration);

        // Add in-memory database
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("TestDatabase"));

        // Register services from WileyWidget.Services.Uno
        // TODO: Add service registrations here

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DatabaseContext_ShouldBeConfigured()
    {
        // Arrange
        var dbContext = _serviceProvider.GetRequiredService<AppDbContext>();

        // Act
        var canConnect = await dbContext.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect);
    }

    [Fact]
    public void Configuration_ShouldLoadTestSettings()
    {
        // Arrange & Act
        var connectionString = _configuration.GetConnectionString("Default");
        var quickBooksEnabled = _configuration.GetValue<bool>("QuickBooks:Enabled");

        // Assert
        Assert.Equal("InMemoryDb", connectionString);
        Assert.False(quickBooksEnabled);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
