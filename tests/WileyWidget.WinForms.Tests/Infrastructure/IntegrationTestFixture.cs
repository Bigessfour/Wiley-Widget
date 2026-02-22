using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SPSE = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions;
using WileyWidget.Data;
using WileyWidget.WinForms.Configuration;
using Xunit;

namespace WileyWidget.WinForms.Tests.Infrastructure;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public sealed class IntegrationTestsCollection : ICollectionFixture<IntegrationTestFixture>
{
}

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private ServiceProvider? _provider;
    private IServiceScope? _fixtureScope;

    public IServiceProvider Services => _provider
        ?? throw new InvalidOperationException("IntegrationTestFixture not yet initialized.");

    public AppDbContext Db => SPSE.GetRequiredService<AppDbContext>(_fixtureScope!.ServiceProvider);

    public IServiceScope CreateScope() => _provider!.CreateScope();

    public Task InitializeAsync()
    {
        InitializeSyncfusionTheme();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["UI:IsUiTestHarness"] = "true",
                ["UI:ShowRibbon"] = "true",
                ["UI:UseSyncfusionDocking"] = "false",
                ["UI:AutoShowDashboard"] = "false",
                ["UI:MinimalMode"] = "false",
                ["XAI:ApiKey"] = "gsk_test_fixture_placeholder",
                ["OPENAI_API_KEY"] = "sk-fake-fixture-placeholder",
                ["Services:QuickBooks:OAuth:RedirectUri"] = "http://localhost:9876/callback"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWinFormsServices(configuration);

        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            // ValidateOnBuild is intentionally disabled: it JIT-compiles ALL service factory
            // lambdas at build time, which triggers loading Microsoft.WinForms.Utilities.Shared
            // (a .NET 6-era framework assembly absent from the .NET 10 WindowsDesktop runtime).
            // ValidateScopes=true is sufficient to catch lifetime mis-matches at test time.
            ValidateOnBuild = false
        });

        _fixtureScope = _provider.CreateScope();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixtureScope?.Dispose();
        if (_provider is not null)
        {
            await _provider.DisposeAsync().AsTask();
        }
    }

    private static void InitializeSyncfusionTheme()
    {
        var ready = new ManualResetEventSlim(false);
        Exception? initException = null;

        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Syncfusion.WinForms.Controls.SfSkinManager.ApplicationVisualTheme =
                    WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;

                var dummyDocking = new Syncfusion.Windows.Forms.Tools.DockingManager();
                dummyDocking.ThemeName = WileyWidget.WinForms.Themes.ThemeColors.DefaultTheme;
                dummyDocking.Dispose();
            }
            catch (Exception ex)
            {
                initException = ex;
            }
            finally
            {
                ready.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(15));

        if (initException is not null)
        {
            throw new InvalidOperationException(
                "Syncfusion theme initialization failed in IntegrationTestFixture.", initException);
        }
    }
}

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    protected IServiceProvider Services => _fixture.Services;
    protected AppDbContext Db => _fixture.Db;
    protected IServiceScope CreateScope() => _fixture.CreateScope();

    protected IntegrationTestBase(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;
}
