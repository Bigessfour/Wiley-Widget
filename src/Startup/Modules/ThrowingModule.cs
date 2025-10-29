using System;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace WileyWidget.Startup.Modules
{
    /// <summary>
    /// Diagnostic test module that can optionally throw during Initialize to validate global error handling.
    /// Controlled by environment variable: THROW_MODULE_INIT_EXCEPTION=1
    /// </summary>
    public class ThrowingModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // No-op
        }

        public void RegisterTypes(Prism.Ioc.IContainerRegistry containerRegistry)
        {
            // No registrations required for this test module
        }

        public void Initialize()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("THROW_MODULE_INIT_EXCEPTION");
                if (!string.IsNullOrEmpty(env) && env == "1")
                {
                    Log.Error("ThrowingModule: Intentionally throwing during Initialize (THROW_MODULE_INIT_EXCEPTION=1)");
                    throw new InvalidOperationException("Test exception: ThrowingModule.Initialize() - intentional test throw");
                }
                else
                {
                    Log.Information("ThrowingModule: Initialized normally (no test exception requested)");
                }
            }
            catch (Exception ex)
            {
                // Surface via Serilog and rethrow so the module initialization pipeline experiences a failure
                Log.Error(ex, "ThrowingModule: Exception during Initialize");
                throw;
            }
        }
    }
}
