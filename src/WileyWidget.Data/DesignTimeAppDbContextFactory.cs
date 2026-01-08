#nullable enable

using System;
using System.IO;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace WileyWidget.Data
{
    /// <summary>
    /// Design-time factory for EF Core tools (dotnet ef).
    /// Loads configuration identically to Program.cs and delegates to the runtime AppDbContextFactory.
    /// This eliminates the WindowsDesktop runtime pack dependency.
    /// </summary>
    public class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        [SuppressMessage("Microsoft.Globalization", "CA1303", Justification = "Design-time diagnostic messages use literal strings")]
        public AppDbContext CreateDbContext(string[] args)
        {
            // Mimic Program.cs environment loading
            Env.Load("secrets/my.secrets"); // Secrets first
            Env.Load();                     // Then .env (overrides if present)

            var basePath = Directory.GetCurrentDirectory();
            Console.WriteLine($"[DesignTime] Loading configuration from base path: {basePath}");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Delegate to your resilient runtime factory
            var runtimeFactory = new AppDbContextFactory(configuration);

            // For design-time, force normal mode (no degraded/in-memory unless explicitly configured)
            // This prevents accidental in-memory migrations
            if (AppDbStartupState.IsDegradedMode)
            {
                Console.WriteLine("[DesignTime] WARNING: Degraded mode detected - migrations will target in-memory. Override via config if unintended.");
            }

            return runtimeFactory.CreateDbContext();
        }
    }
}
