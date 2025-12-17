using System.Reflection;
using System.Windows.Forms;

namespace WileyWidget.McpServer.Helpers;

/// <summary>
/// Caches reflected form types and constructors for improved batch validation performance.
/// </summary>
public static class FormTypeCache
{
    private static readonly Dictionary<string, Type?> _typeCache = new();
    private static readonly Dictionary<Type, ConstructorInfo?> _mainFormConstructorCache = new();
    private static readonly Dictionary<Type, ConstructorInfo?> _parameterlessConstructorCache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Gets a form type by name with caching.
    /// </summary>
    public static Type? GetFormType(string formTypeName)
    {
        lock (_lock)
        {
            if (_typeCache.TryGetValue(formTypeName, out var cachedType))
            {
                return cachedType;
            }

            var assembly = typeof(WileyWidget.WinForms.Forms.MainForm).Assembly;
            var type = assembly.GetType(formTypeName);
            _typeCache[formTypeName] = type;
            return type;
        }
    }

    /// <summary>
    /// Gets the MainForm constructor for a form type with caching.
    /// </summary>
    public static ConstructorInfo? GetMainFormConstructor(Type formType)
    {
        ArgumentNullException.ThrowIfNull(formType);
        lock (_lock)
        {
            if (_mainFormConstructorCache.TryGetValue(formType, out var cachedCtor))
            {
                return cachedCtor;
            }

            var ctor = formType.GetConstructor(new[] { typeof(WileyWidget.WinForms.Forms.MainForm) });
            _mainFormConstructorCache[formType] = ctor;
            return ctor;
        }
    }

    /// <summary>
    /// Gets the parameterless constructor for a form type with caching.
    /// </summary>
    public static ConstructorInfo? GetParameterlessConstructor(Type formType)
    {
        ArgumentNullException.ThrowIfNull(formType);
        lock (_lock)
        {
            if (_parameterlessConstructorCache.TryGetValue(formType, out var cachedCtor))
            {
                return cachedCtor;
            }

            var ctor = formType.GetConstructor(Type.EmptyTypes);
            _parameterlessConstructorCache[formType] = ctor;
            return ctor;
        }
    }

    /// <summary>
    /// Discovers all form types in the WileyWidget.WinForms.Forms namespace with caching.
    /// </summary>
    public static List<Type> GetAllFormTypes()
    {
        lock (_lock)
        {
            var assembly = typeof(WileyWidget.WinForms.Forms.MainForm).Assembly;
            return assembly.GetTypes()
                .Where(t => typeof(Form).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.Namespace == "WileyWidget.WinForms.Forms"
                            && !t.Name.Contains("Base", StringComparison.Ordinal)
                            && !t.Name.Contains("Test", StringComparison.Ordinal)
                            && !t.Name.Contains("Mock", StringComparison.Ordinal))
                .OrderBy(t => t.Name)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all caches (useful for testing or after assembly reload).
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _typeCache.Clear();
            _mainFormConstructorCache.Clear();
            _parameterlessConstructorCache.Clear();
        }
    }
}
