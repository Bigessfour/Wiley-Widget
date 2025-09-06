// SECURE LICENSE TEST PROGRAM
// This program tests the secure license provider functionality

using System;
using WileyWidget.Licensing;

namespace LicenseTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🔐 Testing Secure License Provider");
            Console.WriteLine("==================================");

            // Test license registration
            var success = SecureLicenseProvider.RegisterLicense();

            Console.WriteLine($"\nRegistration Result: {(success ? "SUCCESS" : "FAILED")}");

            // Get license status
            var status = SecureLicenseProvider.GetLicenseStatus();
            Console.WriteLine("\nLicense Status:");
            Console.WriteLine(status);

            // Test environment variable reading
            Console.WriteLine("\nEnvironment Variable Test:");
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                Console.WriteLine("✅ Machine-level environment variable found");
                Console.WriteLine($"   Length: {envKey.Length} characters");
                Console.WriteLine($"   Starts with: {envKey.Substring(0, Math.Min(20, envKey.Length))}...");
            }
            else
            {
                Console.WriteLine("❌ Machine-level environment variable not found");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
