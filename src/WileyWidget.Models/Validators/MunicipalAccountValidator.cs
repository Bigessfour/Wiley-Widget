using FluentValidation;

namespace WileyWidget.Models.Validators;

/// <summary>
/// FluentValidation validator for <see cref="MunicipalAccount"/> entities.
/// Enforces GASB-compliant validation rules before saving to the database.
/// </summary>
public class MunicipalAccountValidator : AbstractValidator<MunicipalAccount>
{
    /// <summary>
    /// Minimum valid account number length (e.g., "1")
    /// </summary>
    private const int MinAccountNumberLength = 1;

    /// <summary>
    /// Maximum account number length per schema constraints
    /// </summary>
    private const int MaxAccountNumberLength = 20;

    /// <summary>
    /// Maximum account name length per schema constraints
    /// </summary>
    private const int MaxAccountNameLength = 100;

    /// <summary>
    /// Maximum notes length per schema constraints
    /// </summary>
    private const int MaxNotesLength = 200;

    public MunicipalAccountValidator()
    {
        // Account Number validation
        RuleFor(x => x.AccountNumber)
            .NotNull()
            .WithMessage("Account number is required.");

        RuleFor(x => x.AccountNumber!.Value)
            .NotEmpty()
            .WithMessage("Account number is required.")
            .When(x => x.AccountNumber != null);

        RuleFor(x => x.AccountNumber!.Value)
            .MinimumLength(MinAccountNumberLength)
            .WithMessage($"Account number must be at least {MinAccountNumberLength} character(s).")
            .MaximumLength(MaxAccountNumberLength)
            .WithMessage($"Account number cannot exceed {MaxAccountNumberLength} characters.")
            .When(x => x.AccountNumber != null && !string.IsNullOrEmpty(x.AccountNumber.Value));

        RuleFor(x => x.AccountNumber!.Value)
            .Matches(@"^\d+([.-]\d+)*$")
            .WithMessage("Account number must be numeric with optional separators (dots or hyphens), e.g., 405, 405.1, 101-1000-000.")
            .When(x => x.AccountNumber != null && !string.IsNullOrEmpty(x.AccountNumber.Value));

        // Name validation
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Account name is required.")
            .MaximumLength(MaxAccountNameLength)
            .WithMessage($"Account name cannot exceed {MaxAccountNameLength} characters.");

        // Department validation
        RuleFor(x => x.DepartmentId)
            .GreaterThan(0)
            .WithMessage("Department selection is required.");

        // Budget Period validation
        RuleFor(x => x.BudgetPeriodId)
            .GreaterThan(0)
            .WithMessage("Budget period selection is required.");

        // Balance validation - decimal type is always valid, just check it's reasonable
        // Note: decimal doesn't have NaN or Infinity, so no special checks needed
        // We just ensure it doesn't exceed reasonable bounds for currency
        RuleFor(x => x.Balance)
            .LessThanOrEqualTo(decimal.MaxValue)
            .WithMessage("Balance must be a valid decimal value.");

        // Budget Amount validation
        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Budget amount cannot be negative.");

        // Type description validation (nullable but has max length)
        RuleFor(x => x.TypeDescription)
            .MaximumLength(50)
            .WithMessage("Type description cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.TypeDescription));

        // Fund description validation (nullable but has max length)
        RuleFor(x => x.FundDescription)
            .MaximumLength(100)
            .WithMessage("Fund description cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.FundDescription));

        // Notes validation (nullable but has max length)
        RuleFor(x => x.Notes)
            .MaximumLength(MaxNotesLength)
            .WithMessage($"Notes cannot exceed {MaxNotesLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        // QuickBooks ID validation (nullable but has max length)
        RuleFor(x => x.QuickBooksId)
            .MaximumLength(50)
            .WithMessage("QuickBooks ID cannot exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.QuickBooksId));

        // Parent account self-reference validation
        RuleFor(x => x)
            .Must(x => x.ParentAccountId != x.Id || x.Id == 0)
            .WithMessage("An account cannot be its own parent.")
            .When(x => x.ParentAccountId.HasValue);
    }
}

/// <summary>
/// Extension methods for validating MunicipalAccount using FluentValidation.
/// </summary>
public static class MunicipalAccountValidationExtensions
{
    private static readonly MunicipalAccountValidator _validator = new();

    /// <summary>
    /// Validates a MunicipalAccount and returns a list of error messages.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>An enumerable of validation error messages, empty if valid.</returns>
    public static IEnumerable<string> Validate(this MunicipalAccount account)
    {
        if (account == null)
        {
            yield return "Account cannot be null.";
            yield break;
        }

        var result = _validator.Validate(account);
        foreach (var error in result.Errors)
        {
            yield return error.ErrorMessage;
        }
    }

    /// <summary>
    /// Validates a MunicipalAccount and throws a <see cref="ValidationException"/> if invalid.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation fails.</exception>
    public static void ValidateAndThrow(this MunicipalAccount account)
    {
        _validator.ValidateAndThrow(account);
    }

    /// <summary>
    /// Validates a MunicipalAccount and returns whether it is valid.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValid(this MunicipalAccount account)
    {
        if (account == null) return false;
        return _validator.Validate(account).IsValid;
    }
}
