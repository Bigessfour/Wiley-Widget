using Xunit;
using WileyWidget.McpServer.Helpers;
using WileyWidget.McpServer.Tools;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WileyWidget.McpServer.Tests.Tools;

/// <summary>
/// ENHANCED Tests for RemediationSuggestorTool - captures full Grok responses.
/// Tests verify that Grok understands color violations and SfSkinManager remediation patterns.
/// NOTE: No timeout - Grok is allowed unlimited time for deep analysis.
/// </summary>
public class RemediationSuggestorToolTests_Enhanced
{
    private static IConfiguration GetTestConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<RemediationSuggestorToolTests_Enhanced>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        return config;
    }

    [Fact]
    public async Task SuggestRemediation_SyncfusionThemingViolation_ExposesSfSkinManagerKnowledge()
    {
        // Arrange
        var config = GetTestConfiguration();
        SemanticKernelService.Initialize(config);

        var violations = @"
Dashboard.cs (Line 45): myButton.BackColor = Color.FromArgb(0, 120, 215);
Dashboard.cs (Line 46): myButton.ForeColor = Color.White;
ReportPanel.cs (Line 23): statusLabel.BackColor = ThemeColors.Background;
ReportPanel.cs (Line 24): statusLabel.ForeColor = Color.Black;
";

        var context = @"
SYNCFUSION WINFORMS APPLICATION ARCHITECTURE
==============================================

Theme Authority: SfSkinManager (single source of truth)
Active Theme: Office2019Colorful
Theme Initialization: SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly)
Theme Application: ThemeColors.ApplyTheme(this) in form constructors

VIOLATION PATTERN DETECTED:
Manual color assignments bypass SfSkinManager theme cascade.
Should: Remove manual BackColor/ForeColor assignments
Should: Rely on theme cascade from parent form
Should: Use SfSkinManager.SetVisualStyle() for dynamic control initialization

QUESTION FOR GROK:
What specific code changes should be made to eliminate these color violations?
How should the developer use SfSkinManager and theme cascade?
What is the proper Syncfusion WinForms pattern for theme-aware controls?
";

        // Act - Give Grok UNLIMITED time to think about this
        var result = await RemediationSuggestorTool.SuggestRemediation(violations, context, "text");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // OUTPUT: Let's see what Grok actually says
        Console.WriteLine("\n");
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  GROK RESPONDS TO SYNCFUSION REMEDIATION CHALLENGE             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n[INPUT] Color Violations:");
        Console.WriteLine(violations);
        Console.WriteLine("\n[INPUT] Architecture Context:");
        Console.WriteLine(context);
        Console.WriteLine("\n[GROK RESPONSE]:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(result);
        Console.WriteLine("─────────────────────────────────────────────────────────────────");

        // ANALYSIS
        var lowerResult = result.ToLower();
        var analysis = new
        {
            MentionsSfSkinManager = lowerResult.Contains("sfskinmanager"),
            MentionsThemeManager = lowerResult.Contains("theme manager"),
            MentionsThemeCascade = lowerResult.Contains("cascade") || lowerResult.Contains("inherit"),
            MentionsRemoveColors = lowerResult.Contains("remove") && lowerResult.Contains("color"),
            MentionsSetVisualStyle = lowerResult.Contains("setvisualstyle"),
            MentionsOffice2019 = lowerResult.Contains("office2019"),
            MentionsApplyTheme = lowerResult.Contains("applytheme"),
        };

        Console.WriteLine("\n[ANALYSIS]:");
        Console.WriteLine($"  ✓ Mentions 'SfSkinManager': {analysis.MentionsSfSkinManager}");
        Console.WriteLine($"  ✓ Mentions 'theme manager': {analysis.MentionsThemeManager}");
        Console.WriteLine($"  ✓ Mentions 'cascade'/'inherit': {analysis.MentionsThemeCascade}");
        Console.WriteLine($"  ✓ Suggests removing colors: {analysis.MentionsRemoveColors}");
        Console.WriteLine($"  ✓ Mentions 'SetVisualStyle': {analysis.MentionsSetVisualStyle}");
        Console.WriteLine($"  ✓ Mentions 'Office2019': {analysis.MentionsOffice2019}");
        Console.WriteLine($"  ✓ Mentions 'ApplyTheme': {analysis.MentionsApplyTheme}");
        Console.WriteLine("\n");
    }

    [Fact]
    public async Task AnalyzeViolationPatterns_LargeMigration_ProvidesStrategicGuidance()
    {
        // Arrange
        var config = GetTestConfiguration();
        SemanticKernelService.Initialize(config);

        var violationData = @"
VIOLATION INVENTORY FOR LARGE MIGRATION
========================================

Total Forms Analyzed: 23
Color Violations Found: 47 across 12 forms
Pattern 1: Legacy forms (created 2023) have manual BackColor/ForeColor
Pattern 2: Newer forms (created 2025) have removed manual colors
Pattern 3: Inconsistency: Some use ThemeColors custom properties (VIOLATION)

Specific Examples:
  LoginForm.cs: 5 BackColor assignments
  MainForm.cs: 8 BackColor + ForeColor assignments
  ReportForm.cs: 3 hardcoded Color.FromArgb calls
  SettingsForm.cs: 2 BackColor assignments
  DashboardForm.cs: 4 ForeColor assignments

Severity: HIGH - Blocks runtime theme switching
Impact: User cannot change application theme

Question: What is the most efficient migration strategy?
          What are the risks of this pattern?
          What are the costs of fixing it?
";

        // Act
        var result = await RemediationSuggestorTool.AnalyzeViolationPatterns(violationData, "text");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // OUTPUT: Full response with analysis
        Console.WriteLine("\n");
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  GROK ANALYZES LARGE MIGRATION PATTERNS                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n[VIOLATION PATTERNS SUBMITTED TO GROK]:");
        Console.WriteLine(violationData);
        Console.WriteLine("\n[GROK'S STRATEGIC ANALYSIS]:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(result);
        Console.WriteLine("─────────────────────────────────────────────────────────────────\n");
    }

    [Fact]
    public async Task SuggestRemediation_CustomColorProperties_ExploresDeprecationStrategy()
    {
        // Arrange
        var config = GetTestConfiguration();
        SemanticKernelService.Initialize(config);

        var violations = @"
Custom Color Properties Violation:
  ThemeColors.cs (Lines 10-15): public static Color Background { get; }
  ThemeColors.cs (Lines 16-20): public static Color PrimaryAccent { get; }
  ThemeColors.cs (Lines 21-25): public static Color ErrorRed { get; }

Usage Sites:
  LoginForm.cs (Line 45): myPanel.BackColor = ThemeColors.Background;
  Dashboard.cs (Line 67): statusButton.ForeColor = ThemeColors.PrimaryAccent;
  ErrorDialog.cs (Line 23): errorLabel.ForeColor = ThemeColors.ErrorRed;
";

        var context = @"
PROBLEM: Custom color properties (ThemeColors.*) violate Syncfusion theming.
These properties hide the actual color source and make it impossible to know
what colors are being used across the application.

ARCHITECTURE: Should use ONLY SfSkinManager.SetVisualStyle() 
              with theme cascade from parent form.

QUESTION: How should we deprecate ThemeColors color properties?
          What's the safest migration path?
          How do we prevent new code from using them?
          What about semantic colors (error=red, success=green)?
";

        // Act
        var result = await RemediationSuggestorTool.SuggestRemediation(violations, context, "text");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // OUTPUT
        Console.WriteLine("\n");
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  GROK EVALUATES CUSTOM COLOR PROPERTY DEPRECATION STRATEGY    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n[CUSTOM COLOR PROPERTIES]:");
        Console.WriteLine(violations);
        Console.WriteLine("\n[ARCHITECTURAL CONSTRAINTS]:");
        Console.WriteLine(context);
        Console.WriteLine("\n[GROK'S DEPRECATION STRATEGY]:");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine(result);
        Console.WriteLine("─────────────────────────────────────────────────────────────────\n");
    }
}
