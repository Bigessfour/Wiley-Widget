# Wiley Widget Municipal Budget Integration Implementation Plan

## Executive Summary

This comprehensive implementation plan addresses the critical gaps identified in Wiley Widget's current data model when compared to real municipal budget structures from the 2026 Town of Wiley and Wiley Sanitation District budgets. The plan transforms Wiley Widget from a basic utility rate calculator into a full-featured municipal financial management system compliant with GASB standards.

## Current State Assessment

### ✅ Strengths

- Solid Entity Framework Core foundation
- Basic fund/account structure (General, Water, Sewer, Trash, Enterprise)
- Account balance and budget tracking
- WPF UI with data binding

### ❌ Critical Gaps

1. **Account Numbering**: No support for hierarchical decimal numbering (405, 410.1, 411, etc.)
2. **Fund Types**: Missing Conservation Trust, Recreation, Capital Projects funds
3. **Account Types**: Only 5 generic types vs. 20+ municipal-specific categories
4. **Department Hierarchy**: Flat structure vs. departmental organization
5. **Excel Import**: No capability to import budget files
6. **Validation**: No GASB-compliant business rules
7. **Multi-Year Tracking**: No budget period management

## Phase 1: Data Model Transformation

### 1.1 Enhanced Fund Types (GASB Compliance)

**Objective**: Expand fund types to match GASB standards and real municipal structures.

**Implementation Steps:**

1. Update `FundType` enum in `MunicipalAccount.cs`:

   ```csharp
   public enum FundType
   {
       // Governmental Funds
       General,
       SpecialRevenue,
       CapitalProjects,
       DebtService,

       // Proprietary Funds
       Enterprise,
       InternalService,

       // Fiduciary Funds
       Trust,
       Agency,

       // Additional Municipal Funds
       ConservationTrust,
       Recreation,
       Utility
   }
   ```

2. Add fund class categorization:

   ```csharp
   public enum FundClass
   {
       Governmental,
       Proprietary,
       Fiduciary,
       Memo
   }
   ```

3. Update `MunicipalAccount` model:

   ```csharp
   public class MunicipalAccount
   {
       // ... existing properties ...

       [Required]
       public FundClass FundClass { get; set; }

       [Required]
       [StringLength(100)]
       public string FundDescription { get; set; }
   }
   ```

**Validation Rules:**

- General Fund must be Governmental class
- Enterprise funds must be Proprietary class
- Conservation Trust must be Fiduciary class

### 1.2 Hierarchical Account Numbering System

**Objective**: Implement GASB-compliant account numbering with hierarchical relationships.

**Implementation Steps:**

1. Create `AccountNumber` value object:

   ```csharp
   [Owned]
   public class AccountNumber
   {
       [Required]
       [RegularExpression(@"^\d+(\.\d+)*$", ErrorMessage = "Account number must be numeric with optional decimal levels")]
       public string Value { get; private set; }

       public int Level => Value.Split('.').Length;
       public string ParentNumber => Level > 1 ? string.Join(".", Value.Split('.').Take(Level - 1)) : null;

       public AccountNumber(string value)
       {
           if (!Regex.IsMatch(value, @"^\d+(\.\d+)*$"))
               throw new ArgumentException("Invalid account number format");

           Value = value;
       }
   }
   ```

2. Update `MunicipalAccount` model:

   ```csharp
   public class MunicipalAccount
   {
       // Replace string AccountNumber with:
       [Required]
       public AccountNumber AccountNumber { get; set; }

       // Add hierarchical relationships
       public int? ParentAccountId { get; set; }
       public MunicipalAccount ParentAccount { get; set; }
       public ICollection<MunicipalAccount> ChildAccounts { get; set; } = new List<MunicipalAccount>();
   }
   ```

3. Add database constraints:

   ```csharp
   // In migration
   builder.Entity<MunicipalAccount>()
       .HasIndex(a => a.AccountNumber.Value)
       .IsUnique();

   builder.Entity<MunicipalAccount>()
       .HasOne(a => a.ParentAccount)
       .WithMany(a => a.ChildAccounts)
       .HasForeignKey(a => a.ParentAccountId)
       .OnDelete(DeleteBehavior.Restrict);
   ```

### 1.3 Department Hierarchy

**Objective**: Implement departmental organization matching municipal structures.

**Implementation Steps:**

1. Create `Department` model:

   ```csharp
   public class Department
   {
       [Key]
       public int Id { get; set; }

       [Required]
       [StringLength(10)]
       public string Code { get; set; } // "GEN GOVT", "HWY&ST", "WATER"

       [Required]
       [StringLength(100)]
       public string Name { get; set; } // "General Government", "Highways & Streets"

       [Required]
       public FundType Fund { get; set; }

       public int? ParentDepartmentId { get; set; }
       public Department ParentDepartment { get; set; }
       public ICollection<Department> ChildDepartments { get; set; } = new List<Department>();

       // Navigation
       public ICollection<MunicipalAccount> Accounts { get; set; } = new List<MunicipalAccount>();
   }
   ```

2. Update `MunicipalAccount`:

   ```csharp
   public class MunicipalAccount
   {
       // ... existing properties ...

       [Required]
       public int DepartmentId { get; set; }
       public Department Department { get; set; }
   }
   ```

### 1.4 Enhanced Account Types

**Objective**: Expand account types to match municipal accounting categories.

**Implementation Steps:**

1. Update `AccountType` enum:

   ```csharp
   public enum AccountType
   {
       // Asset Types
       Cash,
       Investments,
       Receivables,
       Inventory,
       FixedAssets,

       // Liability Types
       Payables,
       Debt,
       AccruedLiabilities,

       // Equity Types
       RetainedEarnings,
       FundBalance,

       // Revenue Types
       Taxes,
       Fees,
       Grants,
       Interest,
       Sales,

       // Expense Types
       Salaries,
       Supplies,
       Services,
       Utilities,
       Maintenance,
       Insurance,
       Depreciation,

       // Municipal-Specific Types
       PermitsAndAssessments,
       ProfessionalServices,
       ContractLabor,
       DuesAndSubscriptions,
       CapitalOutlay,
       Transfers
   }
   ```

2. Add account type validation rules:
   ```csharp
   public class AccountTypeValidator
   {
       public static bool IsValidForFund(AccountType type, FundType fund)
       {
           // Implement GASB compliance rules
           // e.g., CapitalOutlay only valid for Capital Projects fund
       }
   }
   ```

## Phase 2: Excel Import System

### 2.1 Excel Processing Infrastructure

**Objective**: Build robust Excel import capability for budget files.

**Implementation Steps:**

1. Install required packages:

   ```xml
   <PackageReference Include="DocumentFormat.OpenXml" Version="3.0.1" />
   <PackageReference Include="EPPlus" Version="7.1.2" />
   ```

2. Create `IBudgetImporter` interface:

   ```csharp
   public interface IBudgetImporter
   {
       Task<ImportResult> ImportBudgetAsync(Stream excelStream, BudgetImportOptions options);
   }
   ```

3. Implement OpenXML-based importer:

   ```csharp
   public class ExcelBudgetImporter : IBudgetImporter
   {
       public async Task<ImportResult> ImportBudgetAsync(Stream excelStream, BudgetImportOptions options)
       {
           using var document = SpreadsheetDocument.Open(excelStream, false);
           var workbookPart = document.WorkbookPart;

           // Parse worksheets
           var worksheets = workbookPart.Workbook.Descendants<Sheet>();
           var budgetData = new Dictionary<string, List<AccountData>>();

           foreach (var sheet in worksheets)
           {
               var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
               var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

               var accounts = ParseWorksheet(sheet.Name, sheetData);
               budgetData[sheet.Name] = accounts;
           }

           return await ProcessBudgetDataAsync(budgetData, options);
       }
   }
   ```

### 2.2 Budget Data Mapping

**Objective**: Map Excel worksheet data to Wiley Widget data structures.

**Implementation Steps:**

1. Create worksheet parsers for different budget formats:

   ```csharp
   public class TownOfWileyBudgetParser : IBudgetParser
   {
       public IEnumerable<MunicipalAccount> ParseAccounts(SheetData sheetData)
       {
           // Parse TOW format: Account | Description | Prior Year | 7 Month | Estimate | Budget
           foreach (var row in sheetData.Elements<Row>().Skip(9)) // Skip headers
           {
               var cells = row.Elements<Cell>().ToArray();
               if (cells.Length >= 6)
               {
                   var accountNumber = GetCellValue(cells[0]);
                   var description = GetCellValue(cells[1]);
                   var budgetAmount = ParseDecimal(GetCellValue(cells[5]));

                   yield return new MunicipalAccount
                   {
                       AccountNumber = new AccountNumber(accountNumber),
                       Name = description,
                       BudgetAmount = budgetAmount,
                       Fund = MapWorksheetToFund(sheetData.Name)
                   };
               }
           }
       }
   }
   ```

2. Create fund mapping logic:
   ```csharp
   public static class FundMapper
   {
       public static FundType MapWorksheetName(string worksheetName)
       {
           return worksheetName.ToUpper() switch
           {
               "GF SUMM" or "GENERAL FUND SUMMARY" => FundType.General,
               "WATER&ADM" or "WATER" => FundType.Enterprise,
               "ENTERPRISE" => FundType.Enterprise,
               "CON SUMM" => FundType.ConservationTrust,
               "REC" => FundType.Recreation,
               _ => FundType.General
           };
       }
   }
   ```

### 2.3 Import Validation and Error Handling

**Objective**: Ensure data integrity during import process.

**Implementation Steps:**

1. Create validation pipeline:

   ```csharp
   public class BudgetImportValidator
   {
       public ValidationResult ValidateAccount(MunicipalAccount account)
       {
           var result = new ValidationResult();

           // Account number format validation
           if (!Regex.IsMatch(account.AccountNumber.Value, @"^\d+(\.\d+)*$"))
               result.Errors.Add("Invalid account number format");

           // Fund-specific validations
           if (!AccountTypeValidator.IsValidForFund(account.Type, account.Fund))
               result.Errors.Add($"Account type {account.Type} not valid for fund {account.Fund}");

           // Balance validations
           if (account.BudgetAmount < 0 && account.Type == AccountType.Revenue)
               result.Errors.Add("Revenue accounts cannot have negative budget amounts");

           return result;
       }
   }
   ```

2. Implement import transaction handling:

   ```csharp
   public class BudgetImportService
   {
       public async Task<ImportResult> ImportWithTransactionAsync(BudgetImportData data)
       {
           using var transaction = await _context.Database.BeginTransactionAsync();
           try
           {
               // Validate all data first
               var validationResults = data.Accounts.Select(a => _validator.ValidateAccount(a));
               if (validationResults.Any(r => !r.IsValid))
                   return ImportResult.Failure(validationResults.SelectMany(r => r.Errors));

               // Import data
               await _context.MunicipalAccounts.AddRangeAsync(data.Accounts);
               await _context.SaveChangesAsync();

               await transaction.CommitAsync();
               return ImportResult.Success(data.Accounts.Count);
           }
           catch
           {
               await transaction.RollbackAsync();
               throw;
           }
       }
   }
   ```

## Phase 3: Business Logic and Validation

### 3.1 GASB Compliance Rules

**Objective**: Implement Governmental Accounting Standards Board compliance validation.

**Implementation Steps:**

1. Create GASB validator:

   ```csharp
   public class GASBValidator
   {
       public ValidationResult ValidateFundBalance(FundType fund, decimal balance)
       {
           // Governmental funds must have positive fund balance
           if (fund.GetFundClass() == FundClass.Governmental && balance < 0)
               return ValidationResult.Failure("Governmental funds cannot have negative fund balance");

           // Enterprise funds can have negative balances (operating losses)
           return ValidationResult.Success();
       }

       public ValidationResult ValidateAccountNumbering(IEnumerable<MunicipalAccount> accounts)
       {
           var errors = new List<string>();

           // Check for duplicate account numbers
           var duplicates = accounts.GroupBy(a => a.AccountNumber.Value)
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.Key);

           if (duplicates.Any())
               errors.Add($"Duplicate account numbers: {string.Join(", ", duplicates)}");

           // Validate hierarchical relationships
           foreach (var account in accounts.Where(a => a.ParentAccountId.HasValue))
           {
               var parent = accounts.FirstOrDefault(a => a.Id == account.ParentAccountId);
               if (parent == null)
                   errors.Add($"Parent account not found for {account.AccountNumber.Value}");
               else if (!account.AccountNumber.Value.StartsWith(parent.AccountNumber.Value + "."))
                   errors.Add($"Invalid parent-child relationship for {account.AccountNumber.Value}");
           }

           return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
       }
   }
   ```

2. Implement domain services:

   ```csharp
   public class MunicipalAccountingService
   {
       public decimal CalculateFundBalance(FundType fund)
       {
           // Sum all account balances for the fund
           return _context.MunicipalAccounts
               .Where(a => a.Fund == fund)
               .Sum(a => a.Balance);
       }

       public IEnumerable<ValidationResult> ValidateBudgetCompliance()
       {
           var results = new List<ValidationResult>();

           // Check each fund for GASB compliance
           foreach (var fund in Enum.GetValues<FundType>())
           {
               var balance = CalculateFundBalance(fund);
               results.Add(_gasbValidator.ValidateFundBalance(fund, balance));
           }

           return results;
       }
   }
   ```

### 3.2 Budget Period Management

**Objective**: Support multi-year budget tracking and comparisons.

**Implementation Steps:**

1. Create `BudgetPeriod` model:

   ```csharp
   public class BudgetPeriod
   {
       [Key]
       public int Id { get; set; }

       [Required]
       public int Year { get; set; }

       [Required]
       public string Name { get; set; } // "2026 Proposed", "2025 Adopted"

       [Required]
       public DateTime CreatedDate { get; set; }

       [Required]
       public BudgetStatus Status { get; set; }

       public ICollection<MunicipalAccount> Accounts { get; set; } = new List<MunicipalAccount>();
   }

   public enum BudgetStatus
   {
       Draft,
       Proposed,
       Adopted,
       Executed
   }
   ```

2. Update `MunicipalAccount` for period tracking:

   ```csharp
   public class MunicipalAccount
   {
       // ... existing properties ...

       [Required]
       public int BudgetPeriodId { get; set; }
       public BudgetPeriod BudgetPeriod { get; set; }
   }
   ```

## Phase 4: User Interface Enhancements

### 4.1 Hierarchical Account Display

**Objective**: Create tree-view interface for account hierarchy navigation.

**Implementation Steps:**

1. Create `HierarchicalAccountViewModel`:

   ```csharp
   public class HierarchicalAccountViewModel : ViewModelBase
   {
       public MunicipalAccount Account { get; }

       public ObservableCollection<HierarchicalAccountViewModel> Children { get; } = new();

       public bool IsExpanded { get; set; }

       public HierarchicalAccountViewModel(MunicipalAccount account)
       {
           Account = account;
           LoadChildren();
       }

       private void LoadChildren()
       {
           // Load child accounts hierarchically
           var children = _context.MunicipalAccounts
               .Where(a => a.ParentAccountId == Account.Id)
               .OrderBy(a => a.AccountNumber.Value)
               .ToList();

           foreach (var child in children)
           {
               Children.Add(new HierarchicalAccountViewModel(child));
           }
       }
   }
   ```

2. Implement tree view control:
   ```xaml
   <TreeView ItemsSource="{Binding RootAccounts}">
       <TreeView.ItemTemplate>
           <HierarchicalDataTemplate ItemsSource="{Binding Children}">
               <StackPanel Orientation="Horizontal">
                   <TextBlock Text="{Binding Account.AccountNumber.Value}" Margin="0,0,10,0"/>
                   <TextBlock Text="{Binding Account.Name}"/>
                   <TextBlock Text="{Binding Account.BudgetAmount, StringFormat=C}" Margin="10,0,0,0"/>
               </StackPanel>
           </HierarchicalDataTemplate>
       </TreeView.ItemTemplate>
   </TreeView>
   ```

### 4.2 Excel Import UI

**Objective**: Provide user-friendly budget import interface.

**Implementation Steps:**

1. Create import dialog:

   ```xaml
   <Window x:Class="WileyWidget.Views.BudgetImportDialog"
           xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
           Title="Import Budget File">
       <Grid>

           <Grid.RowDefinitions>
               <RowDefinition Height="Auto"/>
               <RowDefinition Height="Auto"/>
               <RowDefinition Height="*"/>
               <RowDefinition Height="Auto"/>
           </Grid.RowDefinitions>

           <TextBlock Text="Select Budget File:" Margin="10"/>
           <Button Content="Browse..." Grid.Row="1" Margin="10"
                   Command="{Binding BrowseCommand}"/>

           <DataGrid Grid.Row="2" ItemsSource="{Binding ImportPreview}"
                    AutoGenerateColumns="False" Margin="10">
               <DataGrid.Columns>
                   <DataGridTextColumn Header="Account Number" Binding="{Binding AccountNumber.Value}"/>
                   <DataGridTextColumn Header="Name" Binding="{Binding Name}"/>
                   <DataGridTextColumn Header="Fund" Binding="{Binding Fund}"/>
                   <DataGridTextColumn Header="Budget Amount" Binding="{Binding BudgetAmount, StringFormat=C}"/>
               </DataGrid.Columns>
           </DataGrid>

           <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="10">
               <Button Content="Import" Command="{Binding ImportCommand}" Margin="0,0,10,0"/>
               <Button Content="Cancel" IsCancel="True"/>
           </StackPanel>
       </Grid>
   </Window>
   ```

2. Implement import preview logic:

   ```csharp
   public class BudgetImportViewModel : ViewModelBase
   {
       public ObservableCollection<MunicipalAccount> ImportPreview { get; } = new();

       public async Task LoadPreviewAsync(string filePath)
       {
           using var stream = File.OpenRead(filePath);
           var importer = new ExcelBudgetImporter();
           var result = await importer.ImportBudgetAsync(stream, new BudgetImportOptions { PreviewOnly = true });

           ImportPreview.Clear();
           foreach (var account in result.Accounts)
           {
               ImportPreview.Add(account);
           }
       }
   }
   ```

## Phase 5: Testing and Validation

### 5.1 Unit Tests for Data Model

**Objective**: Ensure data integrity and business rule compliance.

**Implementation Steps:**

1. Create account numbering tests:

   ```csharp
   [TestClass]
   public class AccountNumberTests
   {
       [TestMethod]
       public void AccountNumber_ValidFormat_CreatesSuccessfully()
       {
           var accountNumber = new AccountNumber("405.1");
           Assert.AreEqual("405.1", accountNumber.Value);
           Assert.AreEqual(2, accountNumber.Level);
           Assert.AreEqual("405", accountNumber.ParentNumber);
       }

       [TestMethod]
       [ExpectedException(typeof(ArgumentException))]
       public void AccountNumber_InvalidFormat_ThrowsException()
       {
           new AccountNumber("ABC");
       }
   }
   ```

2. Create GASB compliance tests:

   ```csharp
   [TestClass]
   public class GASBComplianceTests
   {
       [TestMethod]
       public void GovernmentalFund_NegativeBalance_FailsValidation()
       {
           var validator = new GASBValidator();
           var result = validator.ValidateFundBalance(FundType.General, -1000);

           Assert.IsFalse(result.IsValid);
           Assert.IsTrue(result.Errors.Contains("Governmental funds cannot have negative fund balance"));
       }
   }
   ```

### 5.2 Integration Tests for Excel Import

**Objective**: Validate end-to-end import functionality.

**Implementation Steps:**

1. Create Excel import tests:

   ```csharp
   [TestClass]
   public class ExcelImportTests
   {
       [TestMethod]
       public async Task ImportTownOfWileyBudget_CreatesCorrectAccounts()
       {
           // Arrange
           var testFile = @"TestData\TOW_Budget_2026.xlsx";
           var importer = new ExcelBudgetImporter();

           // Act
           using var stream = File.OpenRead(testFile);
           var result = await importer.ImportBudgetAsync(stream, new BudgetImportOptions());

           // Assert
           Assert.IsTrue(result.Success);
           Assert.IsTrue(result.Accounts.Any(a => a.Fund == FundType.General));
           Assert.IsTrue(result.Accounts.Any(a => a.Fund == FundType.Enterprise));
       }
   }
   ```

## Phase 6: Documentation and Training

### 6.1 User Documentation

**Objective**: Provide comprehensive user guides for municipal staff.

**Implementation Steps:**

1. Create budget import guide
2. Document account numbering standards
3. Provide GASB compliance reference
4. Create troubleshooting guides

### 6.2 Developer Documentation

**Objective**: Maintain code quality and knowledge transfer.

**Implementation Steps:**

1. Update XML documentation comments
2. Create architecture decision records
3. Document validation rules
4. Maintain API documentation

## Implementation Timeline

### Month 1: Foundation

- Data model updates (Fund types, Account numbering, Departments)
- Basic validation rules
- Unit test framework

### Month 2: Excel Import

- OpenXML integration
- Budget parsers for TOW and WSD formats
- Import validation and error handling

### Month 3: Business Logic

- GASB compliance rules
- Budget period management
- Hierarchical account processing

### Month 4: User Interface

- Tree view controls
- Import dialogs
- Enhanced reporting views

### Month 5: Testing & Deployment

- Comprehensive test coverage
- Performance optimization
- Production deployment

## Success Metrics

1. **Data Accuracy**: 100% successful import of 2026 budget files
2. **GASB Compliance**: All validation rules pass for imported data
3. **Performance**: Import 1000+ accounts in < 30 seconds
4. **User Adoption**: Municipal staff can independently import budgets
5. **Maintainability**: Code coverage > 80%, clear documentation

## Risk Mitigation

1. **Data Loss Prevention**: Transaction-based imports with rollback capability
2. **Backward Compatibility**: Gradual migration path for existing data
3. **Performance Monitoring**: Built-in profiling and optimization
4. **User Training**: Comprehensive documentation and guided workflows

## Conclusion

This implementation plan transforms Wiley Widget from a basic utility calculator into a comprehensive municipal financial management system. By addressing the structural gaps identified in the 2026 budget analysis, Wiley Widget will provide small-town mayors with the sophisticated financial tools they need to transform municipal enterprises into self-sustaining businesses.

The phased approach ensures manageable development cycles while maintaining system stability and user productivity throughout the transformation.</content>
<parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\MUNICIPAL_BUDGET_INTEGRATION_IMPLEMENTATION_PLAN.md
