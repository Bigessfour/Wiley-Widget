using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;

namespace WileyWidget.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ColorFromArgbAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "WW0001";

        private static readonly LocalizableString Title = "Avoid Color.FromArgb usage";
        private static readonly LocalizableString MessageFormat = "Avoid using Color.FromArgb; prefer theme via SfSkinManager/ThemeColors";
        private static readonly LocalizableString Description = "Color.FromArgb bypasses theme system and prevents consistent theming. Use SfSkinManager or ThemeColors instead.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var expression = invocation.Expression;

            // Look for member access expressions like Color.FromArgb(...)
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = memberAccess.Name.Identifier.ValueText;
                if (memberName != "FromArgb")
                    return;

                // Resolve the method symbol and check the containing type
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
                if (methodSymbol?.ContainingType != null)
                {
                    var containingType = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                    if (containingType == "System.Drawing.Color" || containingType.EndsWith(".Color", StringComparison.Ordinal))
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
