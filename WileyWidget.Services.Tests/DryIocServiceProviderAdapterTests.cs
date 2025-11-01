using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Xunit;

namespace WileyWidget.Services.Tests
{
    public class DryIocServiceProviderAdapterTests
    {
        private Type AdapterType => typeof(WileyWidget.Services.DryIocServiceProviderAdapter);

        private object CreateAdapterWithResolver(object resolver)
        {
            var ctor = AdapterType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(object) }, null);
            if (ctor == null) throw new InvalidOperationException("Adapter internal constructor not found");
            return ctor.Invoke(new[] { resolver });
        }

        [Fact]
        public void Resolve_WithSingleArgResolver_ReturnsInstance()
        {
            var resolver = new SingleArgResolverForTest();
            var adapter = (IServiceProvider)CreateAdapterWithResolver(resolver);

            var result = adapter.GetService(typeof(object));

            Assert.NotNull(result);
            Assert.Equal("Resolved:System.Object", result);
        }

        [Fact]
        public void Resolve_WithExtraArgResolver_ReturnsNullAndDoesNotThrow()
        {
            var resolver = new ExtraArgResolverForTest();
            var adapter = (IServiceProvider)CreateAdapterWithResolver(resolver);

            var sw = new StringWriter();
            var listener = new TextWriterTraceListener(sw);
            Trace.Listeners.Add(listener);
            try
            {
                var result = adapter.GetService(typeof(object));
                Assert.Null(result);
                listener.Flush();
                var output = sw.ToString();
                Assert.Contains("DryIocServiceProviderAdapter", output, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }

        [Fact]
        public void Resolve_WithThrowingResolver_ReturnsNullAndLogsInnerException()
        {
            var resolver = new ThrowingResolverForTest();
            var adapter = (IServiceProvider)CreateAdapterWithResolver(resolver);

            var sw = new StringWriter();
            var listener = new TextWriterTraceListener(sw);
            Trace.Listeners.Add(listener);
            try
            {
                var result = adapter.GetService(typeof(object));
                Assert.Null(result);
                listener.Flush();
                var output = sw.ToString();
                Assert.Contains("Simulated inner exception from resolver", output, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }

        private class SingleArgResolverForTest { public object Resolve(Type t) => $"Resolved:{t.FullName}"; }
        private class ExtraArgResolverForTest { public object Resolve(Type t, object ctx) => throw new InvalidOperationException("Simulated: extra arg caused failure in resolver"); }
        private class ThrowingResolverForTest { public object Resolve(Type t) { throw new InvalidOperationException("Simulated inner exception from resolver"); } }
    }
}
