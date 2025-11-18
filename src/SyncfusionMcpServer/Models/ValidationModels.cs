namespace SyncfusionMcpServer.Models;

/// <summary>
/// Result of validation operations
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Theme validation result
/// </summary>
public class ThemeValidationResult : ValidationResult
{
    public string? ThemeName { get; set; }
    public List<string> ThemesRegistered { get; set; } = new();
    public List<string> MissingResources { get; set; } = new();
    public bool AppliedSuccessfully { get; set; }
    public string? TargetAssembly { get; set; }
}

/// <summary>
/// DataGrid analysis result
/// </summary>
public class DataGridAnalysisResult : ValidationResult
{
    public List<BindingIssue> BindingIssues { get; set; } = new();
    public List<PerformanceIssue> PerformanceIssues { get; set; } = new();
    public List<BestPracticeViolation> BestPracticeViolations { get; set; } = new();
    public int TotalColumns { get; set; }
    public bool VirtualizationEnabled { get; set; }
}

public class BindingIssue
{
    public string Column { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string? Suggestion { get; set; }
}

public class PerformanceIssue
{
    public string Issue { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class BestPracticeViolation
{
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? DocumentationLink { get; set; }
}

/// <summary>
/// License validation result
/// </summary>
public class LicenseValidationResult : ValidationResult
{
    public string? RegisteredVersion { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> LicensedComponents { get; set; } = new();
    public bool IsRegistered { get; set; }
}

/// <summary>
/// XAML parsing result
/// </summary>
public class XamlParsingResult : ValidationResult
{
    public List<ComponentDescriptor> ComponentsFound { get; set; } = new();
    public List<string> NamespaceIssues { get; set; } = new();
    public List<string> BindingPaths { get; set; } = new();
    public int TotalElements { get; set; }
}

/// <summary>
/// Component descriptor from XAML
/// </summary>
public class ComponentDescriptor
{
    public string Type { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<string> Properties { get; set; } = new();
    public string? Name { get; set; }
    public int LineNumber { get; set; }
}

/// <summary>
/// Comprehensive validation report
/// </summary>
public class ValidationReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string ProjectPath { get; set; } = string.Empty;
    public ThemeValidationResult? ThemeValidation { get; set; }
    public List<DataGridAnalysisResult> DataGridAnalyses { get; set; } = new();
    public List<XamlParsingResult> XamlParsingResults { get; set; } = new();
    public LicenseValidationResult? LicenseValidation { get; set; }
    public ValidationSummary Summary { get; set; } = new();
}

public class ValidationSummary
{
    public int TotalFiles { get; set; }
    public int TotalErrors { get; set; }
    public int TotalWarnings { get; set; }
    public int ComponentsAnalyzed { get; set; }
    public bool OverallSuccess { get; set; }
}
