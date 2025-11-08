using System.Resources;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: ComVisible(false)]

// WPF Performance Optimization: Specify neutral resources language
// MainAssembly location means resources are embedded in the main assembly (default behavior)
// This avoids satellite assembly lookups and improves startup performance.
// Reference: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/application-startup-time#use-the-neutralresourceslanguage-attribute
// Reference: https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-resources-neutralresourceslanguageattribute
[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
