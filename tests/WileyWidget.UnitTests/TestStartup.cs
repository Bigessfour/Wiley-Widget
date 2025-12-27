using System;
using System.Runtime.CompilerServices;

internal static class TestStartup
{
    // Ensure QuickBooks tests never attempt interactive OAuth during automated runs.
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("WW_SKIP_INTERACTIVE", "1");
        Environment.SetEnvironmentVariable("WW_PRINT_AUTH_URL", null);
    }
}
