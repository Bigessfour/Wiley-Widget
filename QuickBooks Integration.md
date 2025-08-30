# QuickBooks Online Integration Reference

## API Documentation & Endpoints

### Official Documentation
- **Main Developer Portal**: https://developer.intuit.com/app/developer/qbo/docs/develop
- **API Explorer**: https://developer.intuit.com/app/developer/qbo/docs/api/accounting
- **Postman Collections**: https://www.postman.com/intuit/workspace/quickbooks-online-api-collections

### Base URLs
- **Production**: `https://quickbooks.api.intuit.com`
- **Sandbox**: `https://sandbox-quickbooks.api.intuit.com`
- **API Version**: v3

### Authentication
- **OAuth 2.0** with Bearer tokens
- **Scopes**: `com.intuit.quickbooks.accounting`
- **Token Refresh**: Every 55 minutes (typical)

### Key Endpoints

#### Classes (Fund Tracking)
```http
POST /v3/company/{companyId}/class
GET  /v3/company/{companyId}/class/{id}
POST /v3/company/{companyId}/class  # Update
GET  /v3/company/{companyId}/query?query=SELECT * FROM Class
```

#### Accounts
```http
POST /v3/company/{companyId}/account
GET  /v3/company/{companyId}/account/{id}
POST /v3/company/{companyId}/account  # Update
GET  /v3/company/{companyId}/query?query=SELECT * FROM Account
```

#### Customers
```http
POST /v3/company/{companyId}/customer
GET  /v3/company/{companyId}/customer/{id}
POST /v3/company/{companyId}/customer  # Update
```

#### Invoices
```http
POST /v3/company/{companyId}/invoice
GET  /v3/company/{companyId}/invoice/{id}
POST /v3/company/{companyId}/invoice  # Update
GET  /v3/company/{companyId}/query?query=SELECT * FROM Invoice WHERE ClassRef='WaterFund'
```

### Request Format
```json
{
  "Accept": "application/json",
  "Content-Type": "application/json",
  "Authorization": "Bearer {access_token}"
}
```

### Rate Limits
- **500 requests per minute** per app per realm
- **800 requests per minute** combined across all APIs

### Error Codes
- **401**: Authentication failed - refresh token
- **403**: Insufficient permissions
- **429**: Rate limit exceeded - retry after delay
- **400**: Bad request - check payload

### SDK & Libraries
- **Intuit .NET SDK**: `IppDotNetSdkForQuickBooksApiV3`
- **OAuth2 Platform Client**: `IppOAuth2PlatformSdk`

### Best Practices
1. **Use Sandbox** for development and testing
2. **Implement token refresh** before expiration
3. **Handle rate limits** with exponential backoff
4. **Use batch operations** for multiple updates
5. **Store SyncToken** for optimistic locking
6. **Enable class tracking** in QBO company settings

### Municipal Accounting Notes
- **Classes** = Funds (WaterFund, SewerFund, etc.)
- **Sub-accounts** for detailed tracking
- **Tag transactions** with ClassRef for fund segregation
- **Reports** filtered by Class for enterprise P&L

### Implementation Status
- ✅ Models updated with QBO fields
- ✅ Service layer implemented
- ✅ UI integration complete
- ✅ Database migration applied
- 🔄 Testing pending (requires QBO credentials)

### Next Steps
1. Obtain QBO developer account
2. Create sandbox company
3. Set user secrets with credentials
4. Test sync operations
5. Implement error handling and retry logic

---

*Last updated: August 29, 2025*
*Reference: Intuit QuickBooks Online API v3 Documentation*

---

# Wiley Widget → QuickBooks Online Integration Plan

## Executive Summary
This comprehensive integration plan transforms Wiley Widget into a QuickBooks Online whisperer, mapping municipal enterprises (Water, Sewer, Trash, Apartments) to QBO's world using classes for fund segregation. While QBO isn't built for full GASB fund accounting, this approach handles self-sustaining enterprises without commingling cash.

**Key Benefits:**
- Municipal compliance through fund segregation
- Automated sync between Wiley and QBO
- Real-time financial visibility
- Reduced manual data entry
- Audit trails and reporting

## 1. Models Mapping: Wiley to QBO Entities

### Enterprise → QBO Class (Fund Tracking)
Classes act as "funds" in municipal accounting—tag transactions per enterprise to run P&L by Class.

**QBO Class Fields:**
- `Id` (string, read-only): Auto-generated
- `Name` (string, max 100 chars): e.g., "WaterFund" from Enterprise.Name
- `Active` (bool): True by default
- `FullyQualifiedName` (string, read-only): For sub-classes, e.g., "WaterFund:Operations"
- `SubClass` (bool): Use for sub-budgets (e.g., under Water: Maintenance)
- `SyncToken` (string): For optimistic locking on updates

**Wiley Implementation:**
```csharp
// Extend Enterprise model with QBO fields
public class Enterprise
{
    // ... existing fields ...
    
    [StringLength(50)]
    public string? QboClassId { get; set; }
    
    public QboSyncStatus QboSyncStatus { get; set; } = QboSyncStatus.Pending;
    
    public DateTime? QboLastSync { get; set; }
}
```

**Sync Logic:**
```csharp
// In QuickBooksService.cs
public async Task<string> SyncEnterpriseToQboClassAsync(Enterprise enterprise)
{
    if (string.IsNullOrEmpty(enterprise.QboClassId))
    {
        // Create new class
        var qbClass = new Class { Name = enterprise.Name + "Fund", Active = true };
        qbClass = ds.Add(qbClass);
        enterprise.QboClassId = qbClass.Id;
    }
    else
    {
        // Update existing class
        var qbClass = ds.FindById(new Class { Id = enterprise.QboClassId });
        qbClass.Name = enterprise.Name + "Fund";
        qbClass = ds.Update(qbClass);
    }
    enterprise.QboSyncStatus = QboSyncStatus.Synced;
    enterprise.QboLastSync = DateTime.UtcNow;
    return qbClass.Id;
}
```

### BudgetInteraction/OverallBudget → QBO Budget + Accounts
Budgets are query-only via API (limitation), so use UI/CSV import workarounds.

**QBO Budget Fields (Read-Only):**
- `Id` (string)
- `Name` (string): e.g., "FY2025 Water Budget"
- `StartDate/EndDate` (date): Fiscal year
- `BudgetType` (enum: ProfitAndLoss, BalanceSheet)
- `BudgetEntryType` (enum: Monthly, Quarterly, Yearly)
- `BudgetDetail` (array): Lines with AccountRef, ClassRef, Amount (decimal)
- `Active` (bool)

**Workaround Strategy:**
1. Compute budgets in Wiley
2. Export CSV: `"Account,Class,Jan,Feb,..."`
3. User imports to QBO manually
4. Query API for validation

### Expenses/Revenues → QBO Accounts
Core for tracking financial transactions.

**QBO Account Fields:**
- `Id` (string)
- `Name` (string, unique): e.g., "WaterRevenue" from MonthlyRevenue
- `AccountType` (enum: Income, Expense, Asset, etc.)
- `Classification` (enum: Asset, Liability, etc.)
- `CurrentBalance` (decimal, read-only)
- `Description` (string): From Enterprise.Notes
- `TaxCodeRef` (ref): For municipal exemptions
- `SubAccount` (bool): For breakdowns

**Best Practice:** Use sub-accounts under parent (e.g., Utilities:Water:Expenses)

## 2. Accounts Setup in QBO (Municipal-Style)

### Chart of Accounts Structure
```
Enterprise Funds (Parent)
├── Water Revenue (Income)
├── Water Expenses (Expense)
├── Sewer Revenue (Income)
├── Sewer Expenses (Expense)
├── Trash Revenue (Income)
├── Trash Expenses (Expense)
└── Apartments Revenue (Income)
    └── Apartments Expenses (Expense)
```

### Bank Sub-Accounts for Fund Isolation
```
Checking (Main)
├── Checking:WaterFund
├── Checking:SewerFund
├── Checking:TrashFund
└── Checking:ApartmentsFund
```

### API Creation Example
```csharp
// Create revenue account
var revenueAccount = new Account
{
    Name = "WaterRevenue",
    AccountType = AccountTypeEnum.Income,
    Classification = AccountClassificationEnum.Revenue,
    Active = true,
    Description = "Water utility revenue from rates"
};
ds.Add(revenueAccount);
```

### Best Practices
- Enable class tracking in QBO Company Settings > Advanced
- Run reports like "Profit & Loss by Class"
- Track depreciation as non-cash expense
- Use batch API calls (up to 30 operations per batch)

## 3. Forms & Fields: Syncing Data

### Key Forms Mapping

#### Invoices (Rates/Billing)
```json
{
  "Line": [
    {
      "Amount": 100.00,
      "DetailType": "SalesItemLineDetail",
      "SalesItemLineDetail": {
        "ItemRef": { "value": "1", "name": "Services" }
      }
    }
  ],
  "CustomerRef": { "value": "1" },
  "ClassRef": { "value": "WaterFund" }
}
```

#### Bills (Expenses)
```json
{
  "VendorRef": { "value": "123" },
  "Line": [
    {
      "Amount": 500.00,
      "DetailType": "AccountBasedExpenseLineDetail",
      "AccountBasedExpenseLineDetail": {
        "AccountRef": { "value": "456" }
      }
    }
  ],
  "ClassRef": { "value": "WaterFund" }
}
```

#### Journal Entries (Adjustments)
```json
{
  "Line": [
    {
      "Amount": 1000.00,
      "DetailType": "JournalEntryLineDetail",
      "JournalEntryLineDetail": {
        "PostingType": "Debit",
        "AccountRef": { "value": "789" }
      }
    },
    {
      "Amount": 1000.00,
      "DetailType": "JournalEntryLineDetail",
      "JournalEntryLineDetail": {
        "PostingType": "Credit",
        "AccountRef": { "value": "101" }
      }
    }
  ]
}
```

### Custom Fields for Municipal Data
QBO supports up to 3 custom fields per entity:
- `CitizenCount` (Number)
- `EnterpriseType` (String)
- `FiscalYear` (Date)

### Sync Fields Strategy
```csharp
// In QuickBooksService.cs
public void MapWileyToQboFields(Enterprise enterprise, Invoice qbInvoice)
{
    qbInvoice.TotalAmt = enterprise.MonthlyRevenue;
    qbInvoice.ClassRef = new ReferenceType { Value = enterprise.QboClassId };
    qbInvoice.Description = $"Monthly billing for {enterprise.Name}";
    
    // Custom field mapping
    qbInvoice.CustomField = new CustomField[] {
        new CustomField { 
            Name = "CitizenCount", 
            Type = "NumberType", 
            NumberValue = enterprise.CitizenCount 
        }
    };
}
```

## 4. Syncfusion Integration: Commands, Views, UI

### MainWindow.xaml Extensions
```xml
<TabItem Header="QBO Sync">
    <DockPanel>
        <StackPanel Orientation="Horizontal" DockPanel.Dock="Top" Margin="4">
            <Button Content="Connect to QBO" Command="{Binding ConnectToQboCommand}" />
            <Button Content="Sync Enterprises" Command="{Binding SyncEnterprisesToQboCommand}" />
            <Button Content="Sync Budget Interactions" Command="{Binding SyncBudgetInteractionsToQboCommand}" />
            <Button Content="Pull Budgets" Command="{Binding PullBudgetsFromQboCommand}" />
            <Button Content="Export Budgets CSV" Command="{Binding ExportBudgetsCsvCommand}" />
            <TextBlock Text="Busy" Visibility="{Binding QboBusy, Converter={StaticResource BoolToVis}}" />
        </StackPanel>
        
        <syncfusion:SfDataGrid ItemsSource="{Binding QboClasses}" AutoGenerateColumns="False">
            <syncfusion:SfDataGrid.Columns>
                <syncfusion:GridTextColumn HeaderText="Class Name" MappingName="Name" />
                <syncfusion:GridTextColumn HeaderText="Fully Qualified Name" MappingName="FullyQualifiedName" />
                <syncfusion:GridCheckBoxColumn HeaderText="Active" MappingName="Active" />
            </syncfusion:SfDataGrid.Columns>
        </syncfusion:SfDataGrid>
    </DockPanel>
</TabItem>
```

### ViewModel Commands
```csharp
// In MainViewModel.cs
[RelayCommand]
private async Task ConnectToQbo()
{
    if (_qb == null) return;
    try
    {
        QboBusy = true;
        // Trigger OAuth flow if needed
        await _qb.EnsureValidTokenAsync();
        await LoadQboDataAsync();
    }
    catch (Exception ex)
    {
        await ShowErrorDialogAsync("QBO Connection Failed", ex.Message);
    }
    finally
    {
        QboBusy = false;
    }
}

[RelayCommand]
private async Task SyncEnterprisesToQbo()
{
    if (_qb == null || _enterpriseViewModel == null) return;
    try
    {
        QboBusy = true;
        foreach (var enterprise in Enterprises)
        {
            await _qb.SyncEnterpriseToQboClassAsync(enterprise);
        }
        await _context.SaveChangesAsync();
        await LoadQboClassesAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to sync enterprises to QBO");
    }
    finally
    {
        QboBusy = false;
    }
}
```

### Progress Indicators
```xml
<syncfusion:SfBusyIndicator IsBusy="{Binding QboBusy}" 
                          BusyContent="Syncing with QuickBooks..." />
```

### Error Handling UI
```csharp
private async Task ShowErrorDialogAsync(string title, string message)
{
    var messageBox = new SfMessageBox
    {
        Message = message,
        Caption = title,
        Buttons = MessageBoxButtons.OK,
        Icon = MessageBoxIcon.Error
    };
    await messageBox.ShowDialogAsync();
}
```

## 5. Full Integration Flow & Code Snippets

### OAuth Setup Implementation
```csharp
// In QuickBooksService.cs
private async Task<string> GetAccessTokenAsync(string authCode)
{
    var client = new HttpClient();
    var response = await client.PostAsync(
        "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer",
        new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("redirect_uri", _redirectUri),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret)
        }));

    var json = await response.Content.ReadAsStringAsync();
    var tokenData = JObject.Parse(json);
    
    // Store tokens securely
    _settings.Current.QboAccessToken = tokenData["access_token"].ToString();
    _settings.Current.QboRefreshToken = tokenData["refresh_token"].ToString();
    _settings.Current.QboTokenExpiry = DateTime.UtcNow.AddSeconds(
        double.Parse(tokenData["expires_in"].ToString()));
    
    _settings.Save();
    return _settings.Current.QboAccessToken;
}
```

### Complete Sync Flow
```csharp
public async Task ExecuteFullSyncAsync()
{
    // 1. Ensure valid token
    await RefreshTokenIfNeededAsync();
    
    // 2. Sync enterprises to classes
    foreach (var enterprise in _enterprises)
    {
        await SyncEnterpriseToQboClassAsync(enterprise);
    }
    
    // 3. Sync budget interactions to accounts
    foreach (var interaction in _budgetInteractions)
    {
        var classId = interaction.PrimaryEnterprise?.QboClassId;
        if (!string.IsNullOrEmpty(classId))
        {
            await SyncBudgetInteractionToQboAccountAsync(interaction, classId);
        }
    }
    
    // 4. Export budgets for manual import
    await ExportBudgetsToCsvAsync();
    
    // 5. Pull updated data from QBO
    await PullBudgetsFromQboAsync();
    await PullAccountsFromQboAsync();
    
    // 6. Save all changes
    await _context.SaveChangesAsync();
}
```

### Batch Operations for Efficiency
```csharp
public async Task BatchSyncOperationsAsync(List<Enterprise> enterprises)
{
    var batchRequest = new Batch
    {
        BatchItems = new List<BatchItem>()
    };
    
    foreach (var enterprise in enterprises)
    {
        var qbClass = new Class { Name = enterprise.Name + "Fund", Active = true };
        batchRequest.BatchItems.Add(new BatchItem
        {
            Operation = OperationEnum.CREATE,
            Class = qbClass
        });
    }
    
    var batchResponse = ds.ProcessBatch(batchRequest);
    
    // Process responses and update local entities
    foreach (var item in batchResponse.BatchItems)
    {
        if (item.Class != null)
        {
            var enterprise = enterprises.First(e => e.Name + "Fund" == item.Class.Name);
            enterprise.QboClassId = item.Class.Id;
            enterprise.QboSyncStatus = QboSyncStatus.Synced;
        }
    }
}
```

### CSV Export for Budget Import
```csharp
public async Task ExportBudgetsToCsvAsync()
{
    var csvContent = new StringBuilder();
    csvContent.AppendLine("Account,Class,Jan,Feb,Mar,Apr,May,Jun,Jul,Aug,Sep,Oct,Nov,Dec");
    
    foreach (var enterprise in _enterprises)
    {
        // Generate monthly budget data
        var monthlyData = CalculateMonthlyBudgetData(enterprise);
        csvContent.AppendLine($"{enterprise.Name}Revenue,{enterprise.Name}Fund,{string.Join(",", monthlyData)}");
    }
    
    var filePath = Path.Combine(_exportPath, $"QBO_Budgets_{DateTime.Now:yyyyMMdd}.csv");
    await File.WriteAllTextAsync(filePath, csvContent.ToString());
    
    // Open file location for user
    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
}
```

### Error Handling & Retry Logic
```csharp
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (Intuit.Ipp.Exception.IdsException ex) when (ex.ErrorCode == "429")
        {
            // Rate limit exceeded
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
            Log.Warning("Rate limit exceeded, retrying in {Delay}s (attempt {Attempt}/{Max})", 
                       delay.TotalSeconds, attempt, maxRetries);
            await Task.Delay(delay);
        }
        catch (Intuit.Ipp.Exception.IdsException ex) when (ex.ErrorCode == "401")
        {
            // Token expired
            Log.Warning("Token expired, refreshing...");
            await RefreshTokenAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Operation failed on attempt {Attempt}/{Max}", attempt, maxRetries);
            if (attempt == maxRetries) throw;
        }
    }
    
    throw new InvalidOperationException("Operation failed after all retries");
}
```

## Testing & Best Practices

### Testing Strategy
1. **Sandbox First**: All development in QBO sandbox environment
2. **Unit Tests**: Mock QBO API responses for service layer testing
3. **Integration Tests**: Full end-to-end sync testing
4. **Load Testing**: Verify rate limit handling

### Best Practices
- **Limit API Calls**: 100/min per realm, batch operations when possible
- **Webhooks**: Subscribe to entity changes for real-time updates
- **Audit Trails**: Log all sync operations for compliance
- **Custom Fields**: Limit to 3 per entity for municipal data
- **Error Recovery**: Implement comprehensive retry and recovery logic

### Municipal Accounting Considerations
- **Fund Segregation**: Classes ensure proper separation of enterprise funds
- **Accrual Accounting**: Align with GASB standards
- **Audit Compliance**: Maintain detailed transaction trails
- **Reporting**: Enable Profit & Loss by Class reporting
- **Budget Variance**: Track actual vs. budgeted performance

---

*Integration Plan Created: August 29, 2025*
*Prepared for Wiley Widget Municipal Enterprise Management System*
*Target: QuickBooks Online API v3 (minor version 75+)*
