using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace WileyWidget.Analyzers
{
    /// <summary>
    /// Analyzer WW0002: Detects MemoryCacheEntryOptions creation without Size property when SizeLimit is configured.
    /// 
    /// Rule: When MemoryCache is configured with SizeLimit, all MemoryCacheEntryOptions must explicitly set Size.
    /// 
    /// Severity: Warning
    /// Category: Caching
    /// 
    /// Reference: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory#use-setsize-size-and-sizelimit-to-limit-cache-size
    /// Quote: "An entry won't be cached if the sum of the cached entry sizes exceeds the value specified by SizeLimit.
    /// If no cache size limit is set, the cache size set on the entry is ignored."
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MemoryCacheSizeRequiredAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "WW0002";
        private const string Category = "Caching";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(AnalyzerResources.WW0002_Title),
            AnalyzerResources.ResourceManager,
            typeof(AnalyzerResources));

        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(AnalyzerResources.WW0002_MessageFormat),
            AnalyzerResources.ResourceManager,
            typeof(AnalyzerResources));

        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(AnalyzerResources.WW0002_Description),
            AnalyzerResources.ResourceManager,
            typeof(AnalyzerResources));

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Check for MemoryCacheEntryOptions instantiation
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            // Check if this is creating MemoryCacheEntryOptions
            var typeName = objectCreation.Type.ToString();
            if (!typeName.Contains("MemoryCacheEntryOptions"))
                return;

            // Check if the object initializer has a Size property
            if (objectCreation.Initializer == null)
            {
                // No initializer - definitely missing Size
                ReportDiagnostic(context, objectCreation);
                return;
            }

            // Check if Size property is set in initializer
            var hasSizeProperty = false;
            foreach (var assignment in objectCreation.Initializer.Expressions)
            {
                if (assignment is AssignmentExpressionSyntax assignmentExpr)
                {
                    var leftSide = assignmentExpr.Left.ToString();
                    if (leftSide == "Size")
                    {
                        hasSizeProperty = true;
                        break;
                    }
                }
            }

            if (!hasSizeProperty)
            {
                ReportDiagnostic(context, objectCreation);
            }
        }

        private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax node)
        {
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
