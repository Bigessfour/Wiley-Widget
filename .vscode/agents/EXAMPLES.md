# Example Scenarios: Using Custom Copilot Agents

This document provides real-world examples of using the custom Copilot agents (**Winnie**, **XPert**, **GIT**) for common development tasks in the Wiley Widget project.

## Table of Contents

1. [Windows Forms Development with Winnie](#windows-forms-development-with-winnie)
2. [Testing with XPert](#testing-with-xpert)
3. [CI/CD with GIT](#cicd-with-git)
4. [Multi-Agent Workflows](#multi-agent-workflows)

---

## Windows Forms Development with Winnie

### Example 1: Creating a New Dashboard Panel

**Goal:** Add a financial dashboard panel with charts and data grids.

**Prompt:**
```
@Winnie Create a new FinancialDashboardPanel with the following requirements:

Structure:
- Top section: 3 metric cards (Total Revenue, Total Expenses, Net Profit)
- Middle section: SfChart showing revenue vs expenses over time
- Bottom section: SfDataGrid showing recent transactions

Architecture:
- Pure MVVM with FinancialDashboardViewModel
- Inject IFinancialRepository for data
- Commands: RefreshData, ExportReport
- ObservableCollection for transactions

Integration:
- Dock to central panel (like other document panels in MainForm.Docking.cs)
- Office2019Colorful theme via SfSkinManager
- Proper ThemeName on all Syncfusion controls

Validation:
- Build with WileyWidget: Build task
- Verify theme consistency
```

**Expected Output:**
- `FinancialDashboardPanel.cs` (UserControl with DataBinding)
- `FinancialDashboardPanel.Designer.cs` (UI layout)
- `FinancialDashboardViewModel.cs` (Business logic, INotifyPropertyChanged)
- DI registration code snippet
- Docking integration code snippet

**Winnie's Approach:**
1. Analyze MainForm.Docking.cs for docking patterns
2. Query Syncfusion MCP for SfChart and SfDataGrid API
3. Generate pristine MVVM files
4. Provide integration instructions
5. Validate with build task

---

### Example 2: Refactoring Legacy Code-Behind to MVVM

**Goal:** Clean up an old panel that has business logic mixed with UI code.

**Prompt:**
```
@Winnie I have this CustomerListPanel with business logic in code-behind event handlers. 
Please refactor it to pure MVVM following our MainForm patterns.

Current code:
- Button click handlers call repository directly
- No ViewModel
- DataGrid bound to DataTable
- No INotifyPropertyChanged

Requirements:
- Extract all logic to CustomerListViewModel
- Use RelayCommand for button actions
- Bind to ObservableCollection<CustomerDto>
- Implement property change notifications
- Keep theme and docking setup intact

[Paste current code]
```

**Expected Output:**
- Refactored `CustomerListPanel.cs` (DataBinding only)
- New `CustomerListViewModel.cs` (All business logic)
- Updated Designer.cs with proper bindings
- Migration guide for any breaking changes

**Winnie's Approach:**
1. Read existing code via MCP filesystem
2. Identify business logic to extract
3. Create ViewModel with ObservableObject base
4. Implement RelayCommands
5. Update DataBinding
6. Remove code-behind logic
7. Validate theme still applies correctly

---

### Example 3: Adding a Complex Multi-Panel Layout

**Goal:** Create a split-panel reporting view with navigation tree and detail panel.

**Prompt:**
```
@Winnie Create a ReportingModule with two-panel layout:

Left Panel (Navigation):
- SfTreeViewAdv showing report categories
- Categories: Financial, Operations, Compliance
- Expand/collapse with icons
- Selection drives right panel content

Right Panel (Detail):
- Dynamic content based on tree selection
- Initially shows ReportListPanel (SfDataGrid)
- Can swap to ReportPreviewPanel (SfRichTextBox)
- Smooth transitions

Architecture:
- ReportingModulePanel (main container)
- ReportingModuleViewModel (orchestrates panels)
- ReportNavigationPanel + ViewModel (tree)
- ReportDetailPanel + ViewModel (detail host)
- Use IPanelNavigationService for panel swapping

DockingManager:
- Main ReportingModulePanel docks to center
- Internal SplitContainerAdv for left/right split
- No separate DockingManager for internals

Theme:
- Consistent Office2019Colorful
- Tree icons match theme
- Panel transitions don't flicker

Commands:
- SelectReportCategory
- LoadReportDetail
- ExportReport

Validation:
- Build and run
- Test panel switching
- Verify theme cascade
```

**Expected Output:**
- `ReportingModulePanel.cs` (Main container)
- `ReportNavigationPanel.cs` (Tree view)
- `ReportDetailPanel.cs` (Detail host)
- 3 ViewModels with proper interaction
- DI registration for all components
- Integration with MainForm.Docking.cs

**Winnie's Approach:**
1. Design panel hierarchy
2. Query Syncfusion MCP for SfTreeViewAdv and SplitContainerAdv
3. Implement ViewModels with command interactions
4. Set up panel navigation service integration
5. Apply theme consistently across all panels
6. Provide detailed integration guide

---

### Example 4: Fixing Theme Inconsistencies

**Goal:** Some controls aren't respecting the application theme.

**Prompt:**
```
@Winnie Several controls in our SettingsPanel aren't matching the Office2019Colorful theme:
- Standard .NET Button showing system gray
- Panel backgrounds are white instead of themed
- SfDataGrid has correct theme but SfButton doesn't

Here's the current code:
[Paste SettingsPanel code]

What's wrong and how do I fix it to match our MainForm.Docking.cs theming approach?
```

**Expected Analysis:**
```
Issue 1: Standard .NET Button doesn't support SfSkinManager theming
Fix: Replace with SfButton (supports ThemeName property)

Issue 2: Manual BackColor assignments override theme cascade
Fix: Remove BackColor/ForeColor assignments, rely on SfSkinManager.SetVisualStyle()

Issue 3: SfButton.ThemeName not set
Fix: Add sfButton1.ThemeName = "Office2019Colorful" or use theme service

Recommended Code:
[Winnie provides corrected code with explanations]
```

**Winnie's Approach:**
1. Identify non-Syncfusion controls (can't be themed)
2. Find manual color assignments (violations)
3. Check ThemeName property on Syncfusion controls
4. Verify SfSkinManager.SetVisualStyle() is called
5. Provide compliant replacement code

---

## Testing with XPert

### Example 5: Unit Testing a ViewModel

**Goal:** Comprehensive tests for a new ViewModel.

**Prompt:**
```
@XPert Create comprehensive unit tests for InvoiceListViewModel:

ViewModel Properties:
- Invoices (ObservableCollection<InvoiceDto>)
- SelectedInvoice (InvoiceDto)
- IsLoading (bool)
- ErrorMessage (string)

Commands:
- LoadInvoicesCommand (async)
- AddInvoiceCommand (async)
- EditInvoiceCommand (async, requires selection)
- DeleteInvoiceCommand (async, requires confirmation)
- RefreshCommand (async)

Dependencies:
- IInvoiceRepository (mock with Moq)
- IDialogService (mock with Moq)

Test Coverage:
- Property change notifications
- Command CanExecute logic
- Async command execution
- Repository interaction (success/failure)
- Error handling and ErrorMessage population
- Collection updates on CRUD operations
- Edge cases (null selection, empty results)

Framework: xUnit v3, AAA pattern
```

**Expected Output:**
- `InvoiceListViewModelTests.cs` with 15-20 test methods
- Fixtures for test data
- Moq setup for dependencies
- Theory tests for edge cases
- Coverage report integration

**XPert's Approach:**
1. Analyze ViewModel code via MCP filesystem
2. Generate xUnit test class
3. Create test fixtures and sample data
4. Mock dependencies with Moq
5. Test each property for INotifyPropertyChanged
6. Test each command (execute, CanExecute, async)
7. Test error scenarios
8. Run tests with runTests tool

---

### Example 6: Integration Testing with Database

**Goal:** Test repository layer with real database operations.

**Prompt:**
```
@XPert Create integration tests for InvoiceRepository:

Methods to Test:
- GetAllAsync()
- GetByIdAsync(int id)
- CreateAsync(InvoiceDto dto)
- UpdateAsync(InvoiceDto dto)
- DeleteAsync(int id)
- SearchByCustomerAsync(string customerName)

Requirements:
- Use in-memory SQLite database
- Seed test data in constructor
- Cleanup after each test
- Test transactions and rollback
- Verify EF Core change tracking

Edge Cases:
- Non-existent ID returns null
- Duplicate creation throws exception
- Update of deleted record fails
- Search with no results returns empty
- Concurrent updates (optimistic locking)

Framework: xUnit v3 with IAsyncLifetime for async setup
```

**Expected Output:**
- `InvoiceRepositoryIntegrationTests.cs`
- In-memory database setup
- Test data seeding
- Comprehensive CRUD tests
- Concurrency tests

**XPert's Approach:**
1. Read repository code
2. Set up in-memory database fixture
3. Generate integration test class
4. Implement async setup/teardown
5. Test CRUD operations
6. Test edge cases and errors
7. Run with runTests tool

---

## CI/CD with GIT

### Example 7: Debugging a Failed Workflow

**Goal:** Find out why the build workflow is failing.

**Prompt:**
```
@GIT Our build workflow failed on PR #27. Can you:
1. Analyze the workflow run logs
2. Identify the root cause
3. Suggest a fix for the workflow YAML

Repository: Bigessfour/Wiley-Widget
Workflow: .github/workflows/build-winforms.yml
```

**Expected Analysis:**
```
Root Cause: NuGet restore timeout on Syncfusion packages
Error: "The operation has timed out" at Syncfusion.Chart.Windows restore

Issue: Default timeout is 60 seconds, Syncfusion packages are large

Fix: Increase NuGet timeout in workflow
[GIT provides YAML patch]
```

**GIT's Approach:**
1. Use github-workflow_listRuns to find failing run
2. Use github-workflow_getRunLogs to fetch logs
3. Analyze error messages
4. Propose workflow fix
5. Validate against best practices

---

### Example 8: Optimizing Build Performance

**Goal:** Speed up CI pipeline execution time.

**Prompt:**
```
@GIT Our CI build takes 12 minutes. Can you analyze and optimize?

Current workflow:
- Restore NuGet packages
- Build solution
- Run all tests
- Publish artifacts

Goals:
- Reduce to under 5 minutes
- Maintain test coverage
- Keep artifact publishing
```

**Expected Recommendations:**
```
Optimization 1: Parallel test execution
Current: Sequential test projects
Fix: Add matrix strategy for test projects

Optimization 2: Cache NuGet packages
Current: Full restore every run
Fix: Add actions/cache@v3 for ~/.nuget/packages

Optimization 3: Incremental build
Current: Full rebuild
Fix: Add -incremental flag to MSBuild

Optimization 4: Selective test runs
Current: All tests every time
Fix: Split into fast unit tests (every commit) and slow integration tests (merge to main)

[GIT provides updated workflow YAML]
```

---

## Multi-Agent Workflows

### Example 9: Complete Feature Development

**Goal:** Add a complete invoice management feature from scratch.

#### Step 1: Design (Winnie)
```
@Winnie Propose a design for an InvoiceManagementModule with:
- Invoice list panel (grid)
- Invoice detail panel (form)
- Invoice preview panel (report)

Include:
- Panel layout and docking strategy
- ViewModel architecture
- Data flow between panels
- Syncfusion controls to use
```

#### Step 2: Implementation (Winnie)
```
@Winnie Implement the InvoiceManagementModule based on your design proposal:
- Create all panels and ViewModels
- Set up DockingManager integration
- Apply Office2019Colorful theme
- Register in DI container
```

#### Step 3: Testing (XPert)
```
@XPert Create comprehensive tests for the InvoiceManagementModule:
- Unit tests for all 3 ViewModels
- Integration tests for InvoiceRepository
- Mock all external dependencies
```

#### Step 4: CI Integration (GIT)
```
@GIT Ensure the new InvoiceManagementModule tests run in CI:
- Verify test discovery
- Check for any workflow adjustments needed
- Validate test coverage reporting
```

#### Step 5: Documentation (You)
```
Update docs/features/INVOICE_MANAGEMENT.md with:
- User guide
- Architecture diagram
- API reference
```

---

### Example 10: Refactoring with Quality Gates

**Goal:** Refactor a legacy panel with full quality assurance.

#### Step 1: Analyze (Winnie)
```
@Winnie Analyze CustomerPanel.cs and identify:
- Code-behind violations
- Theme inconsistencies
- MVVM gaps
- Docking issues

Propose a refactoring plan.
```

#### Step 2: Implement (Winnie)
```
@Winnie Execute the refactoring plan:
- Extract ViewModel
- Fix theme application
- Update docking integration
- Preserve existing functionality
```

#### Step 3: Add Tests (XPert)
```
@XPert The CustomerPanel was just refactored. Add:
- Unit tests for new CustomerViewModel
- Integration tests for CustomerRepository
- Ensure 80%+ coverage on new code
```

#### Step 4: Validate CI (GIT)
```
@GIT Run CI pipeline on the refactored CustomerPanel:
- Verify all tests pass
- Check build time impact
- Validate code coverage thresholds
```

#### Step 5: Manual Testing (You)
```
Manual validation:
- Launch application
- Test CustomerPanel functionality
- Verify theme consistency
- Check docking behavior
```

---

## Tips for Effective Agent Usage

### 1. Provide Context
**Good:**
```
@Winnie Following our MainForm.Docking.cs patterns, create a ReportsPanel 
with SfDataGrid docked to the left panel like DashboardPanel
```

**Bad:**
```
@Winnie Make a reports panel
```

### 2. Reference Existing Code
**Good:**
```
@XPert Create tests similar to DashboardViewModelTests.cs for the new ReportsViewModel
```

**Bad:**
```
@XPert Test this code
```

### 3. Request Validation
**Good:**
```
@Winnie Create the panel, then build and validate theme consistency
```

**Bad:**
```
@Winnie Create the panel [and hope it works]
```

### 4. Ask for Explanations
**Good:**
```
@Winnie Why did you choose SplitContainerAdv instead of a custom layout?
```

This helps you learn and validate the approach.

### 5. Iterate Based on Feedback
**Good:**
```
@Winnie The theme isn't applying to the nested panels. Can you fix that?
```

Agents can refine their solutions based on your feedback.

---

## Common Pitfalls to Avoid

❌ **Don't mix agent responsibilities:**
```
@Winnie Create a panel and set up CI tests [Wrong: mixing Winnie + GIT]
```

✅ **Use the right agent for each task:**
```
@Winnie Create the panel [First]
@XPert Create tests for it [Second]
@GIT Ensure tests run in CI [Third]
```

---

❌ **Don't be vague:**
```
@Winnie Fix the theme
```

✅ **Be specific:**
```
@Winnie The SfButton in SettingsPanel isn't using Office2019Colorful theme. 
Here's the code: [paste]. What's wrong?
```

---

❌ **Don't skip validation:**
```
@Winnie Create the panel [and never build/test it]
```

✅ **Always validate:**
```
@Winnie Create the panel and run the build task to ensure it compiles
```

---

## Conclusion

These agents are powerful tools when used correctly:

- **Winnie**: Your Windows Forms & Syncfusion expert
- **XPert**: Your testing quality guardian  
- **GIT**: Your CI/CD optimization specialist

Use them together for complete feature development, from design to deployment!

**Next Steps:**
- Try the examples above in your own development
- Adapt prompts to your specific needs
- Share successful patterns with the team

---

**Last Updated:** 2026-02-15
**See Also:**
- `README.md` - Full agent documentation
- `QUICK_START.md` - 30-second quick start guide
- `INTEGRATION_GUIDE.md` - Technical integration details
