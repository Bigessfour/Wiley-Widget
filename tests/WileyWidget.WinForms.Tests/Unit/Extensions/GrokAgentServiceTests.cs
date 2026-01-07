using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.WinForms.Services.AI;

namespace WileyWidget.WinForms.Tests.Unit.Extensions
{
    public class GrokAgentServiceTests
    {
        [Fact]
        public async Task GetSimpleResponse_NoApiKey_ReturnsDiagnosticMessage()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var svc = new GrokAgentService(config);

            var result = await svc.GetSimpleResponse("hello");

            Assert.Equal("No API key configured for Grok", result);
        }

        [Fact]
        public async Task RunAgentAsync_NoApiKey_ReturnsDiagnosticMessage()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var svc = new GrokAgentService(config);

            var result = await svc.RunAgentAsync("please review theme rules");

            Assert.Equal("No API key configured for Grok", result);
        }

        [Fact]
        public void Kernel_Instance_NotNull()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var svc = new GrokAgentService(config);

            Assert.NotNull(svc.Kernel);
        }

        [Fact]
        public void Kernel_AutoRegisters_Plugins()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var logger = loggerFactory.CreateLogger<GrokAgentService>();
            var svc = new GrokAgentService(config, logger);

            Assert.True(ContainsValueRecursive(svc.Kernel, "echo", 6), "Kernel should contain plugin function 'echo' after auto-registration.");
        }

        private static bool ContainsValueRecursive(object? obj, string needle, int depth = 6, HashSet<object>? visited = null)
        {
            if (obj == null || depth < 0) return false;
            visited ??= new HashSet<object>();
            if (visited.Contains(obj)) return false;
            visited.Add(obj);

            try
            {
                if (obj is string s)
                {
                    return s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (obj is IDictionary dict)
                {
                    foreach (var key in dict.Keys)
                        if (ContainsValueRecursive(key, needle, depth - 1, visited)) return true;
                    foreach (var val in dict.Values)
                        if (ContainsValueRecursive(val, needle, depth - 1, visited)) return true;
                    return false;
                }

                if (obj is IEnumerable en)
                {
                    foreach (var item in en)
                    {
                        if (ContainsValueRecursive(item, needle, depth - 1, visited)) return true;
                    }
                    return false;
                }

                var type = obj.GetType();
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    try
                    {
                        var v = f.GetValue(obj);
                        if (ContainsValueRecursive(v, needle, depth - 1, visited)) return true;
                    }
                    catch { }
                }

                foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    try
                    {
                        var v = p.GetValue(obj);
                        if (ContainsValueRecursive(v, needle, depth - 1, visited)) return true;
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        [Fact]
        public void AssemblyContainingGrokAgent_ContainsEchoPlugin()
        {
            var assembly = typeof(GrokAgentService).Assembly;
            Assert.Contains(assembly.GetTypes(), t => t.Name == "EchoPlugin");
        }
    }
}
