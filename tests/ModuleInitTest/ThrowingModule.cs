using System;

namespace ModuleInitTest
{
    /// <summary>
    /// Local diagnostic test module that can optionally throw during Initialize to validate global error handling.
    /// Controlled by environment variable: THROW_MODULE_INIT_EXCEPTION=1
    /// This local copy keeps the test harness cross-platform and self-contained.
    /// </summary>
    public class ThrowingModule
    {
        public void Initialize()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("THROW_MODULE_INIT_EXCEPTION");
                if (!string.IsNullOrEmpty(env) && env == "1")
                {
                    Console.Error.WriteLine("ThrowingModule: Intentionally throwing during Initialize (THROW_MODULE_INIT_EXCEPTION=1)");
                    throw new InvalidOperationException("Test exception: ThrowingModule.Initialize() - intentional test throw");
                }
                else
                {
                    Console.WriteLine("ThrowingModule: Initialized normally (no test exception requested)");
                }
            }
            catch (Exception ex)
            {
                // Surface via console and rethrow so the caller experiences a failure
                Console.Error.WriteLine($"ThrowingModule: Exception during Initialize: {ex}");
                throw;
            }
        }
    }
}
