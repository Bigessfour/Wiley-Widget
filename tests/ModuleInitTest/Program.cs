using System;

namespace ModuleInitTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // Allow overriding via args: 'throw' or 'notthrow'
            var enableThrow = Environment.GetEnvironmentVariable("THROW_MODULE_INIT_EXCEPTION") == "1";
            if (args.Length > 0)
            {
                if (string.Equals(args[0], "throw", StringComparison.OrdinalIgnoreCase))
                    enableThrow = true;
                if (string.Equals(args[0], "notthrow", StringComparison.OrdinalIgnoreCase))
                    enableThrow = false;
            }

            Console.WriteLine($"ModuleInitTest: THROW_MODULE_INIT_EXCEPTION={(enableThrow ? "1" : "0")}");
            if (enableThrow)
            {
                Environment.SetEnvironmentVariable("THROW_MODULE_INIT_EXCEPTION", "1");
            }

            try
            {
                // Instantiate and run the local test module directly to simulate Prism module initialization
                var module = new ThrowingModule();
                Console.WriteLine("Invoking ThrowingModule.Initialize()...");
                module.Initialize();
                Console.WriteLine("ThrowingModule.Initialize() completed without throwing.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ThrowingModule.Initialize() threw an exception:");
                Console.WriteLine(ex.ToString());
                return 2;
            }
        }
    }
}
