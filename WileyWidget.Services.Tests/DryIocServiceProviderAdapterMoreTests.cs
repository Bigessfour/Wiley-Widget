using System;
using System.Diagnostics;
using Xunit;

namespace WileyWidget.Services.Tests
{
    public class DryIocServiceProviderAdapterMoreTests
    {
        // Helper: capture Trace output
        private static string CaptureTrace(Action action)
        {
            var sw = new System.IO.StringWriter();
            var listener = new TextWriterTraceListener(sw);
            Trace.Listeners.Add(listener);
            try
            {
                action();
            }
            finally
            {
                Trace.Flush();
                Trace.Listeners.Remove(listener);
                listener.Close();
            }
            return sw.ToString();
        }

        [Fact]
        public void Resolve_WithParamsArray_UsesParamsOverload()
        {
            var resolver = new ResolverWithParams();
            var adapter = CreateAdapterForResolver(resolver);

            var result = adapter.GetService(typeof(string));

            Assert.Equal("params:0", result);
        }

        [Fact]
        public void Resolve_WithValueTypeParameter_ProvidesDefaultValue()
        {
            var resolver = new ResolverWithInt();
            var adapter = CreateAdapterForResolver(resolver);

            var result = adapter.GetService(typeof(int));

            // fallback should supply default(int) == 0
            Assert.Equal("int:0", result);
        }

        [Fact]
        public void Resolve_WithObjectSecondParameter_DoesNotCrashAndLogsArgIfPassed()
        {
            var resolver = new ResolverWithObjectArg();
            var adapter = CreateAdapterForResolver(resolver);

            var trace = CaptureTrace(() => {
                var result = adapter.GetService(typeof(object));
                // adapter is defensive; we accept null or a value, but must not throw
                Assert.True(result == null || result is string);
            });

            // If adapter accidentally passes itself/container as second arg the resolver will record that.
            Assert.Contains("ReceivedArg=", trace, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_ThrowingResolver_LogsInnerException()
        {
            var resolver = new ThrowingResolver();
            var adapter = CreateAdapterForResolver(resolver);

            var trace = CaptureTrace(() => {
                var result = adapter.GetService(typeof(object));
                Assert.Null(result);
            });

            Assert.Contains("Simulated inner exception from resolver", trace, StringComparison.Ordinal);
        }

        // --- resolver stubs ---
        private class ResolverWithParams
        {
            public object Resolve(Type t, params object[] args)
            {
                return "params:" + (args?.Length ?? 0);
            }
        }

        private class ResolverWithInt
        {
            public object Resolve(Type t, int count)
            {
                return "int:" + count;
            }
        }

        private class ResolverWithObjectArg
        {
            public object? Resolve(Type t, object maybe)
            {
                // Write to Trace to let the adapter tests learn what was passed
                Trace.WriteLine($"ResolverWithObjectArg: ReceivedArg={(maybe==null?"<null>":maybe.GetType().FullName)}");
                Trace.WriteLine($"ReceivedArg={maybe}");
                return null;
            }
        }

        private class ThrowingResolver
        {
            public object Resolve(Type t)
            {
                throw new InvalidOperationException("Simulated inner exception from resolver");
            }
        }

        private static IServiceProvider CreateAdapterForResolver(object resolver)
        {
            var adapterType = typeof(WileyWidget.Services.DryIocServiceProviderAdapter);
            // Try to find a non-public constructor that takes object (resolverContext)
            var ctor = adapterType.GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null,
                new Type[] { typeof(object) },
                null);
            if (ctor == null)
                throw new InvalidOperationException("DryIocServiceProviderAdapter does not expose an internal constructor accepting a resolverContext.");

            var instance = ctor.Invoke(new object[] { resolver });
            return (IServiceProvider)instance!;
        }
    }
}
