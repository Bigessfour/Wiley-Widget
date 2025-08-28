# Azure SQL Integration Implementation Guide

## � Connection Methods & Configuration

### Overview

WileyWidget supports multiple database connection methods to accommodate different development and production scenarios. The application automatically selects the appropriate connection method based on environment and configuration availability.

### Connection Method Priority

1. **Environment Variables** (Highest Priority - Production)
2. **Azure Managed Identity** (Production - Recommended)
3. **Connection String from Configuration** (Development/Testing)
4. **LocalDB Fallback** (Development - Default)

---

## 🏠 **Method 1: LocalDB (Development Default)**

### Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Setup Commands

```powershell
# Install LocalDB
choco install sql-server-localdb -y

# Verify installation
sqllocaldb info

# Start LocalDB instance
sqllocaldb start MSSQLLocalDB

# Check database location
sqllocaldb info MSSQLLocalDB
```

### Advantages

- ✅ No external dependencies
- ✅ Works offline
- ✅ Fast development cycles
- ✅ Automatic database creation

### Limitations

- ❌ Single-user only
- ❌ No cloud backup
- ❌ Limited scalability

---

## ☁️ **Method 2: Azure SQL Database (Production)**

### Environment Variables Configuration

```env
# .env file
AZURE_SQL_SERVER=your-server.database.windows.net
AZURE_SQL_DATABASE=WileyWidgetDb
AZURE_SQL_USER=your-admin-user
AZURE_SQL_PASSWORD=your-secure-password
AZURE_SQL_RETRY_ATTEMPTS=3
```

### App Settings Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=tcp:${AZURE_SQL_SERVER},1433;Initial Catalog=${AZURE_SQL_DATABASE};Persist Security Info=False;User ID=${AZURE_SQL_USER};Password=${AZURE_SQL_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Database": {
    "MaxRetryCount": "${AZURE_SQL_RETRY_ATTEMPTS}",
    "MaxRetryDelay": "00:00:30",
    "EnableRetryOnFailure": true
  }
}
```

### Azure CLI Setup Commands

```powershell
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name "wileywidget-rg" --location "East US"

# Create SQL Server
az sql server create --name "wileywidget-sql" --resource-group "wileywidget-rg" --admin-user "adminuser" --admin-password "SecurePass123!"

# Create database
az sql db create --name "WileyWidgetDb" --resource-group "wileywidget-rg" --server "wileywidget-sql" --service-objective "Basic"

# Configure firewall
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

### Automated Setup

```powershell
# Complete Azure setup
.\scripts\setup-azure.ps1 -AzureSubscriptionId "your-subscription-id"

# Test connection
.\scripts\test-database-connection.ps1
```

### Advantages

- ✅ Enterprise-grade features
- ✅ Automatic backups
- ✅ High availability
- ✅ Scalable performance
- ✅ Built-in security features

### Service Tiers

- **Basic**: Development/testing (~$5/month)
- **Standard S0**: Small production (~$15/month)
- **Premium**: High-performance (~$30/month+)

---

## 🔐 **Method 3: Azure Managed Identity (Recommended)**

### Configuration

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureConnection": "Server=${AZURE_SQL_SERVER};Database=${AZURE_SQL_DATABASE};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

### Setup for Azure App Service

```powershell
# Enable managed identity
az webapp identity assign --name "your-app-name" --resource-group "wileywidget-rg"

# Grant database access
az sql server ad-admin create --server "wileywidget-sql" --resource-group "wileywidget-rg" --display-name "App Service" --object-id "<managed-identity-object-id>"
```

### Code Implementation

```csharp
// Use managed identity in DbContext configuration
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});
```

### Advantages

- ✅ No password management
- ✅ Enhanced security
- ✅ Automatic token rotation
- ✅ Azure AD integration
- ✅ Audit trail

### Requirements

- Azure App Service or Azure Functions
- Managed Identity enabled
- Azure AD admin configured for SQL Server

---

## 🔧 **Method 4: Azure Service Principal**

### Configuration

```env
# .env file
AZURE_CLIENT_ID=your-service-principal-id
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_TENANT_ID=your-tenant-id
```

### Connection String

```csharp
// Programmatic connection string construction
var connectionString = $"Server={server};Database={database};Authentication=Active Directory Service Principal;User Id={clientId};Password={clientSecret};Encrypt=True;";
```

### Setup Commands

```powershell
# Create service principal
az ad sp create-for-rbac --name "wileywidget-sp" --role "Contributor" --scopes "/subscriptions/your-subscription-id"

# Grant SQL Server access
az sql server ad-admin create --server "wileywidget-sql" --resource-group "wileywidget-rg" --display-name "Service Principal" --object-id "<service-principal-object-id>"
```

### Use Cases

- CI/CD pipelines
- Automated deployments
- Service-to-service authentication

---

## 🧪 **Testing Database Connections**

### Test LocalDB

```powershell
.\scripts\test-database-connection.ps1 -UseLocalDB
```

### Test Azure SQL

```powershell
# Test with environment variables
.\scripts\test-database-connection.ps1

# Test with specific connection string
.\scripts\test-database-connection.ps1 -ConnectionString "Server=tcp:your-server.database.windows.net,1433;..."

# Create test data
.\scripts\test-database-connection.ps1 -CreateTestData
```

### Connection Validation

```csharp
// Programmatic connection test
using (var connection = new SqlConnection(connectionString))
{
    try
    {
        connection.Open();
        Console.WriteLine("✅ Connection successful");

        var command = new SqlCommand("SELECT @@VERSION", connection);
        var version = command.ExecuteScalar();
        Console.WriteLine($"SQL Server Version: {version}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Connection failed: {ex.Message}");
    }
}
```

---

## ⚙️ **Advanced Configuration Options**

### Connection Pooling

```json
{
  "Database": {
    "MaxPoolSize": 100,
    "MinPoolSize": 5,
    "ConnectionTimeout": 30,
    "CommandTimeout": 30,
    "Pooling": true
  }
}
```

### Retry Logic

```json
{
  "Database": {
    "MaxRetryCount": 3,
    "MaxRetryDelay": "00:00:30",
    "EnableRetryOnFailure": true,
    "RetryOnFailureDelay": "00:00:05"
  }
}
```

### Performance Monitoring

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

---

## 🔒 **Security Best Practices**

### 1. Credential Management

- **Environment Variables**: Store in `.env` files (development)
- **Azure Key Vault**: Store secrets in production
- **Managed Identity**: Use for Azure-hosted applications
- **Regular Rotation**: Rotate passwords every 90 days

### 2. Network Security

- **SSL/TLS**: Always use `Encrypt=True`
- **Firewall Rules**: Restrict to specific IP ranges
- **VNet Integration**: Use private endpoints for enhanced security
- **Azure Defender**: Enable for SQL Server protection

### 3. Access Control

- **Least Privilege**: Grant minimum required permissions
- **Role-Based Access**: Use Azure AD roles
- **Audit Logging**: Enable SQL Server auditing
- **MFA**: Require multi-factor authentication

---

## 🚨 **Troubleshooting Common Issues**

### Connection Timeout

```powershell
# Increase timeout
Connection Timeout=60;

# Check network connectivity
az sql server list --resource-group "wileywidget-rg"
```

### Authentication Failed

```powershell
# Verify Azure CLI login
az account show

# Test managed identity
az webapp identity show --name "your-app" --resource-group "wileywidget-rg"
```

### Firewall Blocked

```powershell
# Add current IP
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "MyIP" --start-ip-address "your-ip" --end-ip-address "your-ip"

# Allow Azure services
az sql server firewall-rule create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

### Database Not Found

```powershell
# List databases
az sql db list --resource-group "wileywidget-rg" --server "wileywidget-sql"

# Create database if missing
az sql db create --resource-group "wileywidget-rg" --server "wileywidget-sql" --name "WileyWidgetDb" --service-objective "Basic"
```

---

## 📊 **Monitoring & Diagnostics**

### Health Checks

```csharp
// Add health checks
services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "Azure SQL Database");

// Health check endpoint
app.MapHealthChecks("/health");
```

### Performance Monitoring

```csharp
// Enable EF Core logging
options.UseSqlServer(connectionString)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging() // Development only
    .EnableDetailedErrors(); // Development only
```

### Azure Monitor Integration

```powershell
# Enable diagnostic settings
az monitor diagnostic-settings create --name "sql-diagnostics" --resource "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Sql/servers/server" --logs '[{"category": "SQLSecurityAuditEvents", "enabled": true}]' --metrics '[{"category": "AllMetrics", "enabled": true}]' --workspace "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.OperationalInsights/workspaces/workspace"
```

---

## 📚 **Additional Resources**

- [Azure SQL Connection Strings](https://learn.microsoft.com/en-us/azure/azure-sql/database/connect-query-content-reference-guide)
- [EF Core Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [Azure Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [Azure SQL Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview)

---

## �📦 **Stable Package Versions (Updated)**

Based on latest stable releases as of August 2025:

```xml
<!-- EF Core 8.0 (Latest LTS) -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.8" />

<!-- Azure Authentication -->
<PackageReference Include="Azure.Identity" Version="1.13.0" />
<PackageReference Include="Microsoft.Identity.Client" Version="4.65.0" />

<!-- Configuration Management -->
<PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="8.0.0" />
```

## 🏗️ **Implementation Architecture**

### **1. Data Models (Models Folder)**

```csharp
// Models/Widget.cs
using System.ComponentModel.DataAnnotations;

namespace WileyWidget.Models
{
    public class Widget
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
    }
}
```

```csharp
// Models/AppDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Widget> Widgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Widget entity
            modelBuilder.Entity<Widget>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Price).HasPrecision(18, 2);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
```

### **2. Database Configuration (Services Folder)**

```csharp
// Services/DatabaseConfig.cs
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace WileyWidget.Services
{
    public static class DatabaseConfig
    {
        public static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = GetConnectionString(configuration);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);

                    sqlOptions.CommandTimeout(30);
                });

                // Enable sensitive data logging in development only
                #if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
                #endif
            });
        }

        private static string GetConnectionString(IConfiguration configuration)
        {
            // Try environment variable first (production)
            var envConnectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");
            if (!string.IsNullOrEmpty(envConnectionString))
            {
                return envConnectionString;
            }

            // Try configuration file (development)
            var configConnectionString = configuration.GetConnectionString("AzureSql");
            if (!string.IsNullOrEmpty(configConnectionString))
            {
                return configConnectionString;
            }

            // Fallback to Azure managed identity (Azure deployment)
            var server = configuration["AzureSql:Server"] ?? "your-server.database.windows.net";
            var database = configuration["AzureSql:Database"] ?? "your-database";

            return $"Server={server};Database={database};Authentication=Active Directory Managed Identity;";
        }
    }
}
```

### **3. Configuration Setup (App.xaml.cs)**

```csharp
// App.xaml.cs (partial update)
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Build configuration
        var configuration = BuildConfiguration();

        // Configure database
        DatabaseConfig.ConfigureDatabase(services, configuration);

        // Add other services
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTransient<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Continue with existing startup code...
    }

    private IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<App>(optional: true); // For development secrets

        return builder.Build();
    }

    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Service provider not initialized");
}
```

### **4. Configuration Files**

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureSql": "Server=your-server.database.windows.net;Database=your-database;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;"
  },
  "AzureSql": {
    "Server": "your-server.database.windows.net",
    "Database": "your-database"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### **5. Development Connection String (secrets.json)**

For development, create a user secrets file:

```json
// secrets.json (development only)
{
  "ConnectionStrings": {
    "AzureSql": "Server=your-server.database.windows.net;Database=your-database;User Id=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

Setup user secrets:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AzureSql" "Server=your-server.database.windows.net;Database=your-database;User Id=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
```

### **6. Repository Pattern Implementation**

```csharp
// Services/WidgetRepository.cs
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    public class WidgetRepository : IWidgetRepository
    {
        private readonly AppDbContext _context;

        public WidgetRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<Widget>> GetAllWidgetsAsync()
        {
            return await _context.Widgets
                .OrderBy(w => w.Name)
                .ToListAsync();
        }

        public async Task<Widget?> GetWidgetByIdAsync(int id)
        {
            return await _context.Widgets.FindAsync(id);
        }

        public async Task<Widget> AddWidgetAsync(Widget widget)
        {
            _context.Widgets.Add(widget);
            await _context.SaveChangesAsync();
            return widget;
        }

        public async Task UpdateWidgetAsync(Widget widget)
        {
            widget.ModifiedDate = DateTime.UtcNow;
            _context.Widgets.Update(widget);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteWidgetAsync(int id)
        {
            var widget = await _context.Widgets.FindAsync(id);
            if (widget != null)
            {
                _context.Widgets.Remove(widget);
                await _context.SaveChangesAsync();
            }
        }
    }

    public interface IWidgetRepository
    {
        Task<IEnumerable<Widget>> GetAllWidgetsAsync();
        Task<Widget?> GetWidgetByIdAsync(int id);
        Task<Widget> AddWidgetAsync(Widget widget);
        Task UpdateWidgetAsync(Widget widget);
        Task DeleteWidgetAsync(int id);
    }
}
```

### **7. ViewModel Integration**

```csharp
// ViewModels/MainViewModel.cs (partial update)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WileyWidget.Services;

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IWidgetRepository _widgetRepository;
        private readonly ILogger<MainViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<Widget> _widgets = new();

        [ObservableProperty]
        private Widget? _selectedWidget;

        [ObservableProperty]
        private bool _isLoading;

        public MainViewModel(IWidgetRepository widgetRepository, ILogger<MainViewModel> logger)
        {
            _widgetRepository = widgetRepository ?? throw new ArgumentNullException(nameof(widgetRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LoadWidgetsCommand = new AsyncRelayCommand(LoadWidgetsAsync);
        }

        public IAsyncRelayCommand LoadWidgetsCommand { get; }

        private async Task LoadWidgetsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading widgets from database");

                var widgets = await _widgetRepository.GetAllWidgetsAsync();
                Widgets.Clear();

                foreach (var widget in widgets)
                {
                    Widgets.Add(widget);
                }

                _logger.LogInformation("Loaded {Count} widgets", widgets.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load widgets");
                // Handle error (show message to user)
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Additional CRUD commands would go here...
    }
}
```

## 🛠️ **Database Migration Setup**

### **1. Initial Migration**

```powershell
# Create initial migration
dotnet ef migrations add InitialCreate --project WileyWidget/WileyWidget.csproj

# Apply migration to database
dotnet ef database update --project WileyWidget/WileyWidget.csproj
```

### **2. Migration Commands Reference**

```powershell
# Add new migration
dotnet ef migrations add MigrationName

# Update database to latest migration
dotnet ef database update

# Revert to specific migration
dotnet ef database update MigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script
dotnet ef migrations script --output migration.sql
```

## 🔐 **Security Best Practices**

### **1. Connection String Security**

- **Development**: Use user secrets or environment variables
- **Production**: Use Azure managed identity or Azure Key Vault
- **Never**: Hardcode connection strings in source code

### **2. Azure Managed Identity Setup**

For Azure App Service deployment:

```csharp
// Use managed identity in production
var credential = new DefaultAzureCredential();
var token = credential.GetToken(new TokenRequestContext(new[] { "https://database.windows.net/.default" }));

// Connection string with managed identity
var connectionString = $"Server=your-server.database.windows.net;Database=your-database;Authentication=Active Directory Managed Identity;";
```

### **3. Environment-Based Configuration**

```csharp
// appsettings.Production.json
{
  "ConnectionStrings": {
    "AzureSql": "Server=prod-server.database.windows.net;Database=prod-database;Authentication=Active Directory Managed Identity;"
  }
}
```

## 🧪 **Testing Setup**

### **1. Test Database Context**

```csharp
// WileyWidget.Tests/TestDbContext.cs
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Tests
{
    public class TestDbContext : AppDbContext
    {
        public TestDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Use in-memory database for testing
                optionsBuilder.UseInMemoryDatabase("TestDatabase");
            }
        }
    }
}
```

### **2. Repository Tests**

```csharp
// WileyWidget.Tests/WidgetRepositoryTests.cs
[TestFixture]
public class WidgetRepositoryTests
{
    private TestDbContext _context;
    private WidgetRepository _repository;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);
        _repository = new WidgetRepository(_context);
    }

    [Test]
    public async Task AddWidgetAsync_ShouldAddWidget()
    {
        // Arrange
        var widget = new Widget { Name = "Test Widget", Price = 10.99m };

        // Act
        var result = await _repository.AddWidgetAsync(widget);

        // Assert
        Assert.That(result.Id, Is.GreaterThan(0));
        Assert.That(result.Name, Is.EqualTo("Test Widget"));
    }
}
```

## 🚀 **Deployment Considerations**

### **1. Azure App Service Configuration**

```json
// Azure App Service application settings
{
  "AZURE_SQL_CONNECTIONSTRING": "Server=prod-server.database.windows.net;Database=prod-database;Authentication=Active Directory Managed Identity;"
}
```

### **2. Managed Identity Setup**

```powershell
# Enable managed identity on App Service
az webapp identity assign --name your-app --resource-group your-rg

# Grant database access
az sql server ad-admin create --server your-server --resource-group your-rg --display-name "App Service" --object-id <managed-identity-object-id>
```

## 📊 **Performance Optimization**

### **1. Connection Pooling**

EF Core automatically handles connection pooling. Configure via connection string:

```csharp
// Connection string with pooling
"Server=your-server.database.windows.net;Database=your-database;Authentication=Active Directory Managed Identity;Max Pool Size=100;Min Pool Size=5;"
```

### **2. Query Optimization**

```csharp
// Efficient queries
public async Task<IEnumerable<Widget>> GetActiveWidgetsAsync()
{
    return await _context.Widgets
        .Where(w => w.IsActive) // Filter in database
        .OrderBy(w => w.Name)
        .AsNoTracking() // Read-only query
        .ToListAsync();
}
```

### **3. Caching Strategy**

```csharp
// Implement caching for frequently accessed data
public async Task<Widget?> GetWidgetByIdCachedAsync(int id)
{
    var cacheKey = $"widget:{id}";

    // Try cache first
    var cached = await _cache.GetStringAsync(cacheKey);
    if (cached != null)
    {
        return JsonSerializer.Deserialize<Widget>(cached);
    }

    // Get from database
    var widget = await _context.Widgets.FindAsync(id);
    if (widget != null)
    {
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(widget),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
    }

    return widget;
}
```

## 🔍 **Monitoring & Diagnostics**

### **1. EF Core Logging**

```csharp
// Configure EF Core logging
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.LogTo(Console.WriteLine, LogLevel.Information);
    options.EnableSensitiveDataLogging(); // Development only
    options.EnableDetailedErrors(); // Development only
});
```

### **2. Health Checks**

```csharp
// Add health checks
services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// Use in endpoint
app.MapHealthChecks("/health");
```

## 📚 **Additional Resources**

- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Azure SQL Documentation](https://learn.microsoft.com/en-us/azure/azure-sql/)
- [Azure Identity Documentation](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [Connection String Parameters](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring)

---

**Implementation Status**: Ready for Phase 2 (Data Layer Integration)
**Next Steps**: Create initial migration, implement repository pattern, update ViewModels
**Testing**: Unit tests for repository, integration tests for database operations
