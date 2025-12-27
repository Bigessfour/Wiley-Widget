namespace WileyWidget.WinForms.Models
{
    /// <summary>
    /// Display model for municipal accounts consumed by WinForms views.
    /// </summary>
    public record MunicipalAccountDisplay
    {
        /// <summary>
        /// Gets the unique identifier for the municipal account.
        /// </summary>
        /// <value>
        /// The unique identifier for the municipal account.
        /// </value>
        public int Id { get; init; }

        /// <summary>
        /// Gets the account number.
        /// </summary>
        /// <value>
        /// The account number for the municipal account.
        /// </value>
        public string AccountNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the account name.
        /// </summary>
        /// <value>
        /// The name of the account.
        /// </value>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the account name (alias for AccountName).
        /// </summary>
        /// <value>
        /// The account name (alias for AccountName).
        /// </value>
        public string Name => AccountName;

        /// <summary>
        /// Gets the optional description of the account.
        /// </summary>
        /// <value>
        /// The optional description of the account.
        /// </value>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the type of the account.
        /// </summary>
        /// <value>
        /// The type of the account.
        /// </value>
        public string AccountType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the account type (alias for AccountType).
        /// </summary>
        /// <value>
        /// The account type (alias for AccountType).
        /// </value>
        public string Type => AccountType;

        /// <summary>
        /// Gets the name of the fund associated with the account.
        /// </summary>
        /// <value>
        /// The name of the fund associated with the account.
        /// </value>
        public string FundName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the fund name (alias for FundName).
        /// </summary>
        /// <value>
        /// The fund name (alias for FundName).
        /// </value>
        public string Fund => FundName;

        /// <summary>
        /// Gets the current balance of the account.
        /// </summary>
        /// <value>
        /// The current balance of the account.
        /// </value>
        public decimal CurrentBalance { get; init; }

        /// <summary>
        /// Gets the current balance (alias for CurrentBalance).
        /// </summary>
        /// <value>
        /// The current balance (alias for CurrentBalance).
        /// </value>
        public decimal Balance => CurrentBalance;

        /// <summary>
        /// Gets the budgeted amount for the account.
        /// </summary>
        /// <value>
        /// The budgeted amount for the account.
        /// </value>
        public decimal BudgetAmount { get; init; }

        /// <summary>
        /// Gets the department associated with the account.
        /// </summary>
        /// <value>
        /// The department name for the municipal account.
        /// </value>
        public string Department { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the account is active.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the account is active; otherwise, <see langword="false"/>.
        /// </value>
        public bool IsActive { get; init; }

        /// <summary>
        /// Gets a value indicating whether the account has a parent account.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the account has a parent account; otherwise, <see langword="false"/>.
        /// </value>
        public bool HasParent { get; init; }
    }
}
