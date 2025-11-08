using System;
using System.Reflection;
using System.Diagnostics;
using Prism.Ioc;

namespace WileyWidget.Services
{
    /// <summary>
    /// Adapter that exposes an <see cref="IServiceProvider"/> surface while allowing
    /// resolution via Prism's <see cref="IContainerProvider"/>. Internally the adapter
    /// can also accept a DryIoc resolver context (internally, via the internal constructor)
    /// using reflection so DryIoc types are not exposed as public API from this assembly.
    /// </summary>
    public sealed class DryIocServiceProviderAdapter : IServiceProvider
    {
        private readonly Func<Type, object?> _resolver;

        /// <summary>
        /// Public ctor intended for use with Prism's container abstractions.
        /// </summary>
        public DryIocServiceProviderAdapter(IContainerProvider containerProvider)
        {
            if (containerProvider is null) throw new ArgumentNullException(nameof(containerProvider));

            _resolver = (type) =>
            {
                if (type == null) throw new ArgumentNullException(nameof(type));
                try
                {
                    // Attempt to resolve directly. Avoid calling the generic IsRegistered<T>
                    // extension with a runtime Type since its type argument cannot be
                    // inferred (causes CS0411). If Resolve throws or fails, swallow and
                    // return null to match IServiceProvider semantics.
                    return containerProvider.Resolve(type);
                }
                catch (Exception ex)
                {
                    // Swallow resolution exceptions but emit lightweight diagnostics for troubleshooting.
                    // We use Trace to avoid adding runtime logging dependencies; these messages will
                    // appear in VS Output or any Trace listeners configured during tests (e.g., CSX harness).
                    try { Trace.WriteLine($"DryIocServiceProviderAdapter: Resolve via IContainerProvider failed for type {type?.FullName}: {ex}"); } catch { }
                }

                return null;
            };
        }

        /// <summary>
        /// Internal constructor for backwards compatibility with code that has a DryIoc resolver
        /// (e.g. a DryIoc.IResolverContext). Uses reflection to call an available Resolve method
        /// so this class does not need a compile-time dependency on DryIoc types in its public
        /// surface.
        /// </summary>
        internal DryIocServiceProviderAdapter(object resolverContext)
        {
            if (resolverContext is null) throw new ArgumentNullException(nameof(resolverContext));

            var resolverType = resolverContext.GetType();


            // If the provided resolverContext is already an IServiceProvider, prefer that
            // so we don't invoke container-specific Resolve overloads via reflection.
            if (resolverContext is IServiceProvider spFallback)
            {
                _resolver = spFallback.GetService;
                return;
            }

            // Try to find a Resolve(Type) or Resolve(Type, ...) method. Prefer an exact
            // Resolve(Type) overload (single parameter) when available to avoid supplying
            // accidental extra arguments to container implementations.
            MethodInfo? resolveMethod = null;

            // First pass: find an overload with exactly one parameter of type System.Type
            foreach (var m in resolverType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (string.Equals(m.Name, "Resolve", StringComparison.Ordinal))
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(Type))
                    {
                        resolveMethod = m;
                        break;
                    }
                }
            }

            // Second pass: accept any Resolve where the first parameter is System.Type
            if (resolveMethod == null)
            {
                foreach (var m in resolverType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (string.Equals(m.Name, "Resolve", StringComparison.Ordinal) && m.GetParameters().Length >= 1)
                    {
                        var p0 = m.GetParameters()[0];
                        if (p0.ParameterType == typeof(Type))
                        {
                            resolveMethod = m;
                            break;
                        }
                    }
                }
            }

            if (resolveMethod == null)
            {
                throw new InvalidOperationException("Provided resolver does not expose a Resolve(Type, ...) method and is not an IServiceProvider.");
            }

            _resolver = (type) =>
            {
                if (type == null) throw new ArgumentNullException(nameof(type));
                try
                {
                    // Build arguments that match the chosen Resolve overload's parameters.
                    // For parameters after the first (Type), provide safe defaults:
                    // - For reference types: null
                    // - For value types: Activator.CreateInstance
                    // - For params arrays: empty array of element type
                    var parameters = resolveMethod.GetParameters();
                    var args = new object?[parameters.Length];
                    if (parameters.Length > 0)
                        args[0] = type;

                    for (int i = 1; i < parameters.Length; ++i)
                    {
                        var p = parameters[i];
                        var pType = p.ParameterType;

                        // Handle params (ParamArray) specially
                        var isParamArray = Attribute.IsDefined(p, typeof(ParamArrayAttribute));
                        if (isParamArray)
                        {
                            var elemType = pType.IsArray ? pType.GetElementType() ?? typeof(object) : typeof(object);
                            args[i] = Array.CreateInstance(elemType, 0);
                            continue;
                        }

                        if (p.IsOptional)
                        {
                            // If a default value is available, use it; otherwise fall back to safe default
                            try
                            {
                                var dv = p.DefaultValue;
                                if (dv != DBNull.Value)
                                {
                                    args[i] = dv;
                                    continue;
                                }
                            }
                            catch { /* ignore and fall through to safe default */ }
                        }

                        // Safe default for parameter types
                        args[i] = pType.IsValueType ? Activator.CreateInstance(pType) : null;
                    }

                    // Try the simplest invocation first: pass only the Type argument.
                    // This avoids supplying accidental extra arguments that some container
                    // implementations (or caller adapters) might interpret incorrectly
                    // (e.g., Prism's DryIoc container extension). If the method requires
                    // more parameters and will not accept a single-arg call, fall back
                    // to building a full args array as before.
                    try
                    {
                        var invokeResult = resolveMethod.Invoke(resolverContext, new object?[] { type });
                        return invokeResult;
                    }
                    catch (TargetParameterCountException)
                    {
                        // Fall through to legacy behavior: construct defaults for remaining params
                    }
                    catch (TargetInvocationException tie)
                    {
                        // If the target threw, surface a diagnostic but do not rethrow (IServiceProvider contract)
                        try { Trace.WriteLine($"DryIocServiceProviderAdapter: Resolve(Type) invocation threw for {type?.FullName}: {tie.InnerException ?? tie}"); } catch { }
                        return null;
                    }

                    var result = resolveMethod.Invoke(resolverContext, args);
                    return result;
                }
                catch (Exception ex)
                {
                    // On any failure, return null to match IServiceProvider semantics, but emit diagnostics
                    try { Trace.WriteLine($"DryIocServiceProviderAdapter: Resolve via resolverContext failed for type {type?.FullName}: {ex}"); } catch { }
                    return null;
                }
            };
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return _resolver(serviceType);
        }
    }
}
