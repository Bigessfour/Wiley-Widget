using FluentValidation;
using WileyWidget.Models;

namespace WileyWidget.Services.Validation;

/// <summary>
/// Validator for ChatMessage to ensure safe input to AI services.
/// Prevents injection attacks and enforces reasonable constraints.
/// </summary>
public class ChatMessageValidator : AbstractValidator<ChatMessage>
{
    public ChatMessageValidator()
    {
        // Validate message content
        RuleFor(m => m.Message)
            .NotEmpty().WithMessage("Message cannot be empty")
            .MaximumLength(5000).WithMessage("Message cannot exceed 5000 characters")
            .Must(msg => !ContainsInjectionPatterns(msg))
            .WithMessage("Message contains potentially dangerous patterns");

        // Validate timestamp is reasonable
        RuleFor(m => m.Timestamp)
            .GreaterThanOrEqualTo(DateTime.UtcNow.AddHours(-24))
            .WithMessage("Message timestamp is too old")
            .LessThanOrEqualTo(DateTime.UtcNow.AddSeconds(10))
            .WithMessage("Message timestamp is in the future");
    }

    /// <summary>
    /// Check for common injection patterns.
    /// </summary>
    private static bool ContainsInjectionPatterns(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        // Check for SQL injection patterns
        var sqlPatterns = new[]
        {
            "'; DROP", "'; DELETE", "'; UPDATE", "'; INSERT",
            "1=1", "1' OR '1", "admin'--", "' OR 1=1"
        };

        var lowerMsg = message.ToLowerInvariant();
        return sqlPatterns.Any(pattern => lowerMsg.Contains(pattern.ToLowerInvariant()));
    }
}

/// <summary>
/// Validator for ToolCall to ensure safe tool execution.
/// Validates tool names, argument types, and limits.
/// </summary>
public class ToolCallValidator : AbstractValidator<ToolCall>
{
    private static readonly HashSet<string> AllowedTools = new()
    {
        "get_budget_data",
        "analyze_budget_trends",
        "get_account_details",
        "generate_report",
        "read",
        "grep",
        "search",
        "list"
    };

    private const int MaxArgumentCount = 10;
    private const int MaxArgumentValueLength = 1000;

    public ToolCallValidator()
    {
        RuleFor(t => t.Name)
            .NotEmpty().WithMessage("Tool name cannot be empty")
            .Must(name => AllowedTools.Contains(name))
            .WithMessage($"Tool '{0}' is not allowed. Allowed tools: {string.Join(", ", AllowedTools)}");

        RuleFor(t => t.Id)
            .NotEmpty().WithMessage("Tool call ID cannot be empty")
            .MaximumLength(100).WithMessage("Tool call ID is too long");

        RuleFor(t => t.Arguments)
            .NotNull().WithMessage("Arguments cannot be null")
            .Must(args => args.Count <= MaxArgumentCount)
            .WithMessage($"Too many arguments (max: {MaxArgumentCount})")
            .ForEach(arg =>
            {
                arg.Custom((value, context) =>
                {
                    if (value.Value is string strValue && strValue.Length > MaxArgumentValueLength)
                    {
                        context.AddFailure($"Argument '{value.Key}' value exceeds maximum length ({MaxArgumentValueLength})");
                    }

                    // Additional validation for path-like arguments
                    if (value.Key == "path" && value.Value is string pathValue)
                    {
                        if (pathValue.Contains("..") || pathValue.Contains("~"))
                        {
                            context.AddFailure($"Argument '{value.Key}' contains invalid path characters");
                        }
                    }
                });
            });

        RuleFor(t => t.ToolType)
            .NotEmpty().WithMessage("Tool type cannot be empty")
            .Must(type => type == "client_side_tool" || type == "api_tool")
            .WithMessage("Invalid tool type");
    }
}

/// <summary>
/// Validator for ConversationHistory to ensure data integrity.
/// </summary>
public class ConversationHistoryValidator : AbstractValidator<ConversationHistory>
{
    public ConversationHistoryValidator()
    {
        RuleFor(c => c.ConversationId)
            .NotEmpty().WithMessage("Conversation ID cannot be empty")
            .MaximumLength(100).WithMessage("Conversation ID is too long");

        RuleFor(c => c.Title)
            .NotEmpty().WithMessage("Title cannot be empty")
            .MaximumLength(200).WithMessage("Title is too long");

        RuleFor(c => c.Description)
            .MaximumLength(1000).WithMessage("Description is too long");

        RuleFor(c => c.MessagesJson)
            .NotEmpty().WithMessage("Messages cannot be empty")
            .MaximumLength(1000000).WithMessage("Messages JSON is too large (max 1MB)");

        RuleFor(c => c.CreatedAt)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Created date cannot be in the future");

        RuleFor(c => c.UpdatedAt)
            .GreaterThanOrEqualTo(c => c.CreatedAt)
            .WithMessage("Updated date must be after created date");

        RuleFor(c => c.MessageCount)
            .GreaterThanOrEqualTo(0).WithMessage("Message count cannot be negative")
            .LessThanOrEqualTo(10000).WithMessage("Message count exceeds maximum");

        RuleFor(c => c.ToolCallCount)
            .GreaterThanOrEqualTo(0).WithMessage("Tool call count cannot be negative")
            .LessThanOrEqualTo(1000).WithMessage("Tool call count exceeds maximum");
    }
}
