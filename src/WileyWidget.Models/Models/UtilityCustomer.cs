#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WileyWidget.Models;

/// <summary>
/// Represents a municipal utility customer for billing and service management
/// </summary>
public class UtilityCustomer : INotifyPropertyChanged, IValidatableObject
{
    /// <summary>
    /// Property changed event for data binding
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyChanged for specific properties
    /// </summary>
    protected void OnPropertyChanged(params string[] propertyNames)
    {
        if (propertyNames == null) throw new ArgumentNullException(nameof(propertyNames));

        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Default constructor
    /// </summary>
    public UtilityCustomer()
    {
        // Ensure string properties are initialized to empty strings
        _accountNumber = string.Empty;
        _firstName = string.Empty;
        _lastName = string.Empty;
        _companyName = string.Empty;
        _serviceAddress = string.Empty;
        _serviceCity = string.Empty;
        _serviceState = string.Empty;
        _serviceZipCode = string.Empty;
        _mailingAddress = string.Empty;
        _mailingCity = string.Empty;
        _mailingState = string.Empty;
        _mailingZipCode = string.Empty;
        _phoneNumber = string.Empty;
        _emailAddress = string.Empty;
        _meterNumber = string.Empty;
        _taxId = string.Empty;
        _businessLicenseNumber = string.Empty;
        _notes = string.Empty;
    }

    /// <summary>
    /// Unique identifier for the customer
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private string _accountNumber = string.Empty;

    /// <summary>
    /// Customer account number (unique identifier for billing)
    /// </summary>
    [Required(ErrorMessage = "Account number is required")]
    [StringLength(20, ErrorMessage = "Account number cannot exceed 20 characters")]
    public string AccountNumber
    {
        get => _accountNumber;
        set
        {
            if (_accountNumber != value)
            {
                _accountNumber = value;
                OnPropertyChanged(nameof(AccountNumber), nameof(DisplayName));
            }
        }
    }

    private string _firstName = string.Empty;

    /// <summary>
    /// Customer's first name
    /// </summary>
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (_firstName != value)
            {
                _firstName = value;
                OnPropertyChanged(nameof(FirstName), nameof(FullName), nameof(DisplayName));
            }
        }
    }

    private string _lastName = string.Empty;

    /// <summary>
    /// Customer's last name
    /// </summary>
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName
    {
        get => _lastName;
        set
        {
            if (_lastName != value)
            {
                _lastName = value;
                OnPropertyChanged(nameof(LastName), nameof(FullName), nameof(DisplayName));
            }
        }
    }

    private string? _companyName = string.Empty;

    /// <summary>
    /// Company/business name (for commercial accounts)
    /// </summary>
    [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters")]
    public string? CompanyName
    {
        get => _companyName;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_companyName, sanitized, StringComparison.Ordinal))
            {
                _companyName = sanitized;
                OnPropertyChanged(nameof(CompanyName), nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Full name combining first and last name
    /// </summary>
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Display name for UI (company name if available, otherwise full name)
    /// </summary>
    [NotMapped]
    public string DisplayName => !string.IsNullOrWhiteSpace(CompanyName) ? CompanyName! : FullName;

    private CustomerType _customerType;

    /// <summary>
    /// Type of customer account (Residential, Commercial, etc.)
    /// </summary>
    [Required]
    public CustomerType CustomerType
    {
        get => _customerType;
        set
        {
            if (_customerType != value)
            {
                _customerType = value;
                OnPropertyChanged(nameof(CustomerType), nameof(CustomerTypeDescription));
            }
        }
    }

    /// <summary>
    /// Description of the customer type
    /// </summary>
    [NotMapped]
    public string CustomerTypeDescription => CustomerType switch
    {
        CustomerType.Residential => "Residential",
        CustomerType.Commercial => "Commercial",
        CustomerType.Industrial => "Industrial",
        CustomerType.Institutional => "Institutional",
        CustomerType.Government => "Government",
        CustomerType.MultiFamily => "Multi-Family",
        _ => "Unknown"
    };

    private string _serviceAddress = string.Empty;

    /// <summary>
    /// Physical service address
    /// </summary>
    [Required(ErrorMessage = "Service address is required")]
    [StringLength(200, ErrorMessage = "Service address cannot exceed 200 characters")]
    public string ServiceAddress
    {
        get => _serviceAddress;
        set
        {
            if (_serviceAddress != value)
            {
                _serviceAddress = value;
                OnPropertyChanged(nameof(ServiceAddress));
            }
        }
    }

    private string _serviceCity = string.Empty;

    /// <summary>
    /// Service address city
    /// </summary>
    [Required(ErrorMessage = "Service city is required")]
    [StringLength(50, ErrorMessage = "Service city cannot exceed 50 characters")]
    public string ServiceCity
    {
        get => _serviceCity;
        set
        {
            if (_serviceCity != value)
            {
                _serviceCity = value;
                OnPropertyChanged(nameof(ServiceCity));
            }
        }
    }

    private string _serviceState = string.Empty;

    /// <summary>
    /// Service address state/province
    /// </summary>
    [Required(ErrorMessage = "Service state is required")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Service state must be exactly 2 characters")]
    public string ServiceState
    {
        get => _serviceState;
        set
        {
            if (_serviceState != value)
            {
                _serviceState = value;
                OnPropertyChanged(nameof(ServiceState));
            }
        }
    }

    private string _serviceZipCode = string.Empty;

    /// <summary>
    /// Service address ZIP/postal code
    /// </summary>
    [Required(ErrorMessage = "Service ZIP code is required")]
    [StringLength(10, ErrorMessage = "Service ZIP code cannot exceed 10 characters")]
    public string ServiceZipCode
    {
        get => _serviceZipCode;
        set
        {
            if (_serviceZipCode != value)
            {
                _serviceZipCode = value;
                OnPropertyChanged(nameof(ServiceZipCode));
            }
        }
    }

    private string? _mailingAddress = string.Empty;

    /// <summary>
    /// Mailing address (if different from service address)
    /// </summary>
    [StringLength(200, ErrorMessage = "Mailing address cannot exceed 200 characters")]
    public string? MailingAddress
    {
        get => _mailingAddress;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_mailingAddress, sanitized, StringComparison.Ordinal))
            {
                _mailingAddress = sanitized;
                OnPropertyChanged(nameof(MailingAddress));
            }
        }
    }

    private string? _mailingCity = string.Empty;

    /// <summary>
    /// Mailing address city
    /// </summary>
    [StringLength(50, ErrorMessage = "Mailing city cannot exceed 50 characters")]
    public string? MailingCity
    {
        get => _mailingCity;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_mailingCity, sanitized, StringComparison.Ordinal))
            {
                _mailingCity = sanitized;
                OnPropertyChanged(nameof(MailingCity));
            }
        }
    }

    private string? _mailingState = string.Empty;

    /// <summary>
    /// Mailing address state/province
    /// </summary>
    [StringLength(2, ErrorMessage = "Mailing state must be 2 characters")]
    public string? MailingState
    {
        get => _mailingState;
        set
        {
            var sanitized = NormalizeOptional(value)?.ToUpperInvariant();
            if (!string.Equals(_mailingState, sanitized, StringComparison.Ordinal))
            {
                _mailingState = sanitized;
                OnPropertyChanged(nameof(MailingState));
            }
        }
    }

    private string? _mailingZipCode = string.Empty;

    /// <summary>
    /// Mailing address ZIP/postal code
    /// </summary>
    [StringLength(10, ErrorMessage = "Mailing ZIP code cannot exceed 10 characters")]
    public string? MailingZipCode
    {
        get => _mailingZipCode;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_mailingZipCode, sanitized, StringComparison.Ordinal))
            {
                _mailingZipCode = sanitized;
                OnPropertyChanged(nameof(MailingZipCode));
            }
        }
    }

    private string? _phoneNumber = string.Empty;

    /// <summary>
    /// Primary phone number
    /// </summary>
    [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
    public string? PhoneNumber
    {
        get => _phoneNumber;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_phoneNumber, sanitized, StringComparison.Ordinal))
            {
                _phoneNumber = sanitized;
                OnPropertyChanged(nameof(PhoneNumber));
            }
        }
    }

    private string? _emailAddress;

    /// <summary>
    /// Email address for billing notifications (optional; when provided must be valid)
    /// </summary>
    [StringLength(100, ErrorMessage = "Email address cannot exceed 100 characters")]
    public string? EmailAddress
    {
        get => _emailAddress;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_emailAddress, sanitized, StringComparison.OrdinalIgnoreCase))
            {
                _emailAddress = sanitized;
                OnPropertyChanged(nameof(EmailAddress));
            }
        }
    }

    /// <summary>
    /// Custom validation for optional email address
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(EmailAddress))
        {
            var emailAttr = new EmailAddressAttribute();
            if (!emailAttr.IsValid(EmailAddress))
            {
                yield return new ValidationResult("Invalid email address format", new[] { nameof(EmailAddress) });
            }
        }
        if (!string.IsNullOrWhiteSpace(PhoneNumber) && !Regex.IsMatch(PhoneNumber, @"^\+?[0-9\-()\s]{7,20}$"))
        {
            yield return new ValidationResult("Phone number must contain only digits, spaces, parentheses, or dashes", new[] { nameof(PhoneNumber) });
        }

        if (!string.IsNullOrWhiteSpace(MailingState) && MailingState.Length != 2)
        {
            yield return new ValidationResult("Mailing state must be exactly two characters", new[] { nameof(MailingState) });
        }

        if (!string.IsNullOrWhiteSpace(MailingZipCode) && !Regex.IsMatch(MailingZipCode, @"^\d{5}(-\d{4})?$"))
        {
            yield return new ValidationResult("Mailing ZIP code must be 5 digits or ZIP+4", new[] { nameof(MailingZipCode) });
        }

        if (!string.IsNullOrWhiteSpace(TaxId) && !Regex.IsMatch(TaxId, @"^(\d{9}|\d{2}-\d{7})$"))
        {
            yield return new ValidationResult("Tax ID must be 9 digits or in the format 12-3456789", new[] { nameof(TaxId) });
        }

        if (!string.IsNullOrWhiteSpace(BusinessLicenseNumber) && BusinessLicenseNumber.Length < 5)
        {
            yield return new ValidationResult("Business license number must be at least 5 characters", new[] { nameof(BusinessLicenseNumber) });
        }
    }

    private string? _meterNumber;

    /// <summary>
    /// Utility meter number for service identification
    /// </summary>
    [StringLength(20, ErrorMessage = "Meter number cannot exceed 20 characters")]
    public string? MeterNumber
    {
        get => _meterNumber;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_meterNumber, sanitized, StringComparison.Ordinal))
            {
                _meterNumber = sanitized;
                OnPropertyChanged(nameof(MeterNumber));
            }
        }
    }

    private ServiceLocation _serviceLocation;

    /// <summary>
    /// Whether the service is inside or outside city limits
    /// </summary>
    [Required]
    public ServiceLocation ServiceLocation
    {
        get => _serviceLocation;
        set
        {
            if (_serviceLocation != value)
            {
                _serviceLocation = value;
                OnPropertyChanged(nameof(ServiceLocation), nameof(ServiceLocationDescription));
            }
        }
    }

    /// <summary>
    /// Description of the service location
    /// </summary>
    [NotMapped]
    public string ServiceLocationDescription => ServiceLocation switch
    {
        ServiceLocation.InsideCityLimits => "Inside City Limits",
        ServiceLocation.OutsideCityLimits => "Outside City Limits",
        _ => "Unknown"
    };

    private CustomerStatus _status;

    /// <summary>
    /// Current status of the customer account
    /// </summary>
    [Required]
    public CustomerStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status), nameof(StatusDescription), nameof(IsActive));
            }
        }
    }

    /// <summary>
    /// Description of the customer status
    /// </summary>
    [NotMapped]
    public string StatusDescription => Status switch
    {
        CustomerStatus.Active => "Active",
        CustomerStatus.Inactive => "Inactive",
        CustomerStatus.Suspended => "Suspended",
        CustomerStatus.Closed => "Closed",
        _ => "Unknown"
    };

    /// <summary>
    /// Whether the customer account is currently active
    /// </summary>
    [NotMapped]
    public bool IsActive => Status == CustomerStatus.Active;

    private DateTime _accountOpenDate;

    /// <summary>
    /// Date the account was opened
    /// </summary>
    [Required]
    public DateTime AccountOpenDate
    {
        get => _accountOpenDate;
        set
        {
            if (_accountOpenDate != value)
            {
                _accountOpenDate = value;
                OnPropertyChanged(nameof(AccountOpenDate));
            }
        }
    }

    private DateTime? _accountCloseDate;

    /// <summary>
    /// Date the account was closed (if applicable)
    /// </summary>
    public DateTime? AccountCloseDate
    {
        get => _accountCloseDate;
        set
        {
            if (!Equals(_accountCloseDate, value))
            {
                _accountCloseDate = value;
                OnPropertyChanged(nameof(AccountCloseDate));
            }
        }
    }

    private decimal _currentBalance;

    /// <summary>
    /// Current account balance
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentBalance
    {
        get => _currentBalance;
        set
        {
            if (_currentBalance != value)
            {
                _currentBalance = value;
                OnPropertyChanged(nameof(CurrentBalance), nameof(FormattedBalance));
            }
        }
    }

    /// <summary>
    /// Formatted balance for display
    /// </summary>
    [NotMapped]
    public string FormattedBalance => CurrentBalance.ToString("C", CultureInfo.InvariantCulture);

    private string? _taxId;

    /// <summary>
    /// Tax ID or Social Security Number (for commercial accounts)
    /// </summary>
    [StringLength(20, ErrorMessage = "Tax ID cannot exceed 20 characters")]
    public string? TaxId
    {
        get => _taxId;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_taxId, sanitized, StringComparison.Ordinal))
            {
                _taxId = sanitized;
                OnPropertyChanged(nameof(TaxId));
            }
        }
    }

    private string? _businessLicenseNumber;

    /// <summary>
    /// Business license number (for commercial accounts)
    /// </summary>
    [StringLength(20, ErrorMessage = "Business license number cannot exceed 20 characters")]
    public string? BusinessLicenseNumber
    {
        get => _businessLicenseNumber;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_businessLicenseNumber, sanitized, StringComparison.Ordinal))
            {
                _businessLicenseNumber = sanitized;
                OnPropertyChanged(nameof(BusinessLicenseNumber));
            }
        }
    }

    private string? _notes;

    /// <summary>
    /// Additional notes about the customer
    /// </summary>
    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes
    {
        get => _notes;
        set
        {
            var sanitized = NormalizeOptional(value);
            if (!string.Equals(_notes, sanitized, StringComparison.Ordinal))
            {
                _notes = sanitized;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    private DateTime? _connectDate;

    /// <summary>
    /// Date the customer was connected to service
    /// </summary>
    public DateTime? ConnectDate
    {
        get => _connectDate;
        set
        {
            if (!Equals(_connectDate, value))
            {
                _connectDate = value;
                OnPropertyChanged(nameof(ConnectDate));
            }
        }
    }

    private DateTime? _disconnectDate;

    /// <summary>
    /// Date the customer was disconnected from service
    /// </summary>
    public DateTime? DisconnectDate
    {
        get => _disconnectDate;
        set
        {
            if (!Equals(_disconnectDate, value))
            {
                _disconnectDate = value;
                OnPropertyChanged(nameof(DisconnectDate));
            }
        }
    }

    private decimal _lastPaymentAmount;

    /// <summary>
    /// Amount of the last payment made
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LastPaymentAmount
    {
        get => _lastPaymentAmount;
        set
        {
            if (_lastPaymentAmount != value)
            {
                _lastPaymentAmount = value;
                OnPropertyChanged(nameof(LastPaymentAmount));
            }
        }
    }

    private DateTime? _lastPaymentDate;

    /// <summary>
    /// Date of the last payment
    /// </summary>
    public DateTime? LastPaymentDate
    {
        get => _lastPaymentDate;
        set
        {
            if (!Equals(_lastPaymentDate, value))
            {
                _lastPaymentDate = value;
                OnPropertyChanged(nameof(LastPaymentDate));
            }
        }
    }

    /// <summary>
    /// Date the record was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Date the record was last modified
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;
}

/// <summary>
/// Types of customer accounts
/// </summary>
public enum CustomerType
{
    [Description("Residential")]
    Residential,

    [Description("Commercial")]
    Commercial,

    [Description("Industrial")]
    Industrial,

    [Description("Agricultural")]
    Agricultural,

    [Description("Institutional")]
    Institutional,

    [Description("Government")]
    Government,

    [Description("Multi-Family")]
    MultiFamily
}

/// <summary>
/// Service location classification
/// </summary>
public enum ServiceLocation
{
    [Description("Inside City Limits")]
    InsideCityLimits,

    [Description("Outside City Limits")]
    OutsideCityLimits
}

/// <summary>
/// Customer account status
/// </summary>
public enum CustomerStatus
{
    [Description("Active")]
    Active,

    [Description("Inactive")]
    Inactive,

    [Description("Suspended")]
    Suspended,

    [Description("Closed")]
    Closed
}
