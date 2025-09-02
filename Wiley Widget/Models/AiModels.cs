#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WileyWidget.Models;

/// <summary>
/// Stores the results of AI analysis sessions for audit and historical reference
/// </summary>
public class AiAnalysisResult
{
    /// <summary>
    /// Unique identifier for the analysis result
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// When this analysis was performed
    /// </summary>
    [Required]
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of analysis performed (e.g., "BudgetAnalysis", "TrendAnalysis", "GrantAnalysis")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string AnalysisType { get; set; } = string.Empty;

    /// <summary>
    /// Input data hash for caching and duplicate detection
    /// </summary>
    [Required]
    [StringLength(64)]
    public string InputHash { get; set; } = string.Empty;

    /// <summary>
    /// Raw AI response from Grok API
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string AiResponse { get; set; } = string.Empty;

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    [Required]
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Whether the analysis was successful
    /// </summary>
    [Required]
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if analysis failed
    /// </summary>
    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// API call cost or usage metrics
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ApiCost { get; set; }

    /// <summary>
    /// Notes about the analysis
    /// </summary>
    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Stores specific AI-generated recommendations for enterprises
/// </summary>
public class AiRecommendation
{
    /// <summary>
    /// Unique identifier for the recommendation
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the enterprise this recommendation applies to
    /// </summary>
    [Required]
    public int EnterpriseId { get; set; }

    /// <summary>
    /// Navigation property to the enterprise
    /// </summary>
    [ForeignKey(nameof(EnterpriseId))]
    public virtual Enterprise? Enterprise { get; set; }

    /// <summary>
    /// When this recommendation was generated
    /// </summary>
    [Required]
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of recommendation (e.g., "RateHike", "GrantApplication", "CostReduction")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string RecommendationType { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (High, Medium, Low)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// The recommendation text
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string RecommendationText { get; set; } = string.Empty;

    /// <summary>
    /// Expected impact (financial amount)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedImpact { get; set; }

    /// <summary>
    /// Confidence level of the recommendation (0-100)
    /// </summary>
    [Range(0, 100)]
    public int ConfidenceLevel { get; set; }

    /// <summary>
    /// Implementation status
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Implementation deadline
    /// </summary>
    public DateTime? ImplementationDeadline { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Audit trail for AI operations and user interactions
/// </summary>
public class AiAnalysisAudit
{
    /// <summary>
    /// Unique identifier for the audit entry
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// When this audit entry was created
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of operation (e.g., "AnalysisStarted", "ApiCall", "ResultSaved")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// User or system that performed the operation
    /// </summary>
    [StringLength(100)]
    public string UserId { get; set; } = "System";

    /// <summary>
    /// Enterprise ID if operation was enterprise-specific
    /// </summary>
    public int? EnterpriseId { get; set; }

    /// <summary>
    /// Navigation property to the enterprise
    /// </summary>
    [ForeignKey(nameof(EnterpriseId))]
    public virtual Enterprise? Enterprise { get; set; }

    /// <summary>
    /// Description of the operation
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string Metadata { get; set; } = "{}";

    /// <summary>
    /// IP address or system identifier
    /// </summary>
    [StringLength(50)]
    public string Source { get; set; } = "LocalSystem";

    /// <summary>
    /// Operation duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    [Required]
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Persistent cache for AI responses to reduce API calls
/// </summary>
public class AiResponseCache
{
    /// <summary>
    /// Cache key (hash of input parameters)
    /// </summary>
    [Key]
    [StringLength(64)]
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// Cached AI response
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// When this cache entry was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this cache entry expires
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Number of times this cache entry has been accessed
    /// </summary>
    [Required]
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Last time this cache entry was accessed
    /// </summary>
    [Required]
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this cache entry is still valid
    /// </summary>
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
