# Quick Reference: WileyWidget MCP Test Server Usage

## When to Use These Tools

### ValidateFormTheme
**When:** You need to check if a single form violates SfSkinManager theming rules.

**Example Prompts:**
- "Check if MainForm has manual color violations"
- "Validate AccountsForm theme compliance"
- "Does SettingsForm use SfSkinManager correctly?"

**Tool Call:**
```
mcp_wileywidget-u_ValidateFormTheme(
  formTypeName: "WileyWidget.WinForms.Forms.MainForm",
  expectedTheme: "Office2019Colorful"
)
```

---

### InspectSfDataGrid
**When:** You need to see SfDataGrid configuration (columns, data binding, theme).

**Example Prompts:**
- "Inspect the accounts grid on AccountsForm"
- "Show me the columns in the budget grid"
- "What data is bound to the transactions grid?"

**Tool Call:**
```
mcp_wileywidget-u_InspectSfDataGrid(
  formTypeName: "WileyWidget.WinForms.Forms.AccountsForm",
  gridName: "sfDataGridAccounts",
  includeSampleData: true
)
```

---

### BatchValidateForms
**When:** You need to validate all forms in the project or a subset of forms.

**Example Prompts:**
- "Validate all forms for theme compliance"
- "Check which forms have manual color violations"
- "Generate an HTML report of theme validation"

**Tool Call:**
```
mcp_wileywidget-u_BatchValidateForms(
  formTypeNames: null,  // null = all forms
  expectedTheme: "Office2019Colorful",
  failFast: false,
  outputFormat: "html"
)
```

---

### EvalCSharp
**When:** You need to run ad-hoc C# code to test forms or Syncfusion controls.

**Example Prompts:**
- "Test if MainForm initializes correctly with docking enabled"
- "Check if AccountsForm loads theme properly"
- "Run a quick test of the grid data binding"

**Tool Call:**
```
mcp_wileywidget-u_EvalCSharp(
  csx: @"
    var mockMainForm = MockFactory.CreateMockMainForm();
    var form = new MainForm(mockMainForm);
    SfSkinManager.LoadAssembly(typeof(Office2019Theme).Assembly);
    Console.WriteLine($\"Form created: {form.Text}\");
    return true;
  "
)
```

---

## Common Workflows

### 1. Fix Theme Violations on a Form

**Steps:**
1. Run `ValidateFormTheme` to identify violations
2. Open the form's `.cs` file
3. Remove manual `BackColor`/`ForeColor` assignments
4. Ensure `ThemeColors.ApplyTheme(this)` is called in constructor
5. Re-run `ValidateFormTheme` to confirm fix

**Copilot Prompt:**
```
"Fix manual color violations on AccountsForm using ValidateFormTheme to verify"
```

---

### 2. Validate All Forms Before PR

**Steps:**
1. Run `BatchValidateForms` with `outputFormat: "json"`
2. Check for failed forms
3. Fix violations using workflow above
4. Re-run batch validation
5. Commit when all pass

**Copilot Prompt:**
```
"Run batch validation on all forms and show which ones failed"
```

---

### 3. Debug Grid Configuration

**Steps:**
1. Run `InspectSfDataGrid` to see current state
2. Check column mappings and data binding
3. Use `EvalCSharp` to test changes
4. Apply fixes to form code
5. Re-run `InspectSfDataGrid` to verify

**Copilot Prompt:**
```
"Inspect the accounts grid and tell me if the columns are configured correctly"
```

---

### 4. Test Form Constructor Changes

**Steps:**
1. Write test code in `EvalCSharp`
2. Verify form instantiates correctly
3. Check theme application
4. Apply changes to form
5. Run unit tests

**Copilot Prompt:**
```
"Test if AccountsForm constructor works with MainForm parameter using EvalCSharp"
```

---

## Error Handling

### "Form type not found"
- Check that form name is fully qualified: `WileyWidget.WinForms.Forms.FormName`
- Ensure form exists in the project

### "Failed to instantiate"
- Form may have custom constructor requirements beyond MainForm parameter
- Check form constructor signature
- Use `EvalCSharp` to test instantiation manually

### "Failed to load form"
- Form may have initialization errors
- Check form's `InitializeComponent()` method
- Look for missing dependencies or services

### Manual color violations
- Remove `BackColor`/`ForeColor` assignments
- Exception: Semantic status colors (Color.Red/Green/Orange) are allowed
- Use `SfSkinManager.SetVisualStyle()` instead

---

## Output Format Options

### Text (Default)
Human-readable output with emoji indicators (✅/❌).

**Use When:** Running interactively or viewing in terminal

### JSON
Structured data, machine-parsable.

**Use When:**
- CI/CD pipelines
- Parsing results in scripts
- Automated reporting

### HTML (BatchValidateForms only)
Visual dashboard with color-coded results.

**Use When:**
- Generating reports for stakeholders
- Need visual overview of validation status
- Archiving validation history

---

## Performance Tips

1. **Use form type caching:** Repeated calls to same forms are faster
2. **Batch validate in CI/CD:** Run once per PR, not per commit
3. **Filter forms by namespace:** Validate only changed forms when possible
4. **Use failFast:** Stop on first failure in development
5. **Disable sample data:** Set `includeSampleData: false` for faster grid inspection

---

## Integration Examples

### VS Code Task
```json
{
  "label": "Validate All Forms",
  "type": "shell",
  "command": "dotnet",
  "args": [
    "run",
    "--project",
    "tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj",
    "--",
    "BatchValidateForms",
    "null",
    "Office2019Colorful",
    "false",
    "text"
  ]
}
```

### PowerShell Script
```powershell
$result = dotnet run --project tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj -- `
  ValidateFormTheme "WileyWidget.WinForms.Forms.MainForm" "Office2019Colorful" "json"

$json = $result | ConvertFrom-Json
if (-not $json.passed) {
  Write-Error "Theme validation failed"
  exit 1
}
```

---

## Copilot Integration Tips

### Best Prompts for Copilot

**❌ Vague:**
```
"Check the forms"
```

**✅ Specific:**
```
"Run BatchValidateForms to check all forms for manual color violations and show results in JSON format"
```

**❌ Missing Context:**
```
"Validate theme"
```

**✅ Full Context:**
```
"Use ValidateFormTheme on WileyWidget.WinForms.Forms.AccountsForm with Office2019Colorful theme and output as JSON"
```

### Copilot Can Help With

- ✅ Running tools with correct parameters
- ✅ Interpreting validation results
- ✅ Suggesting fixes for violations
- ✅ Generating batch validation scripts
- ✅ Creating CI/CD integration code

### Copilot Cannot (Use Manual Testing)

- ❌ See actual visual appearance of forms
- ❌ Test user interactions
- ❌ Validate business logic
- ❌ Check runtime performance

---

## Quick Troubleshooting Checklist

- [ ] Is form name fully qualified? (`Namespace.FormName`)
- [ ] Does form have MainForm constructor or parameterless constructor?
- [ ] Is form in WileyWidget.WinForms.Forms namespace?
- [ ] Is form abstract or a base class? (Not validatable)
- [ ] Are you using latest version of MCP server tools?
- [ ] Have you built the project after code changes?
- [ ] Is SfSkinManager loaded in production code?

---

## Related Documentation

- **Full Technical Reference:** `tools/WileyWidgetMcpServer/README.md`
- **MCP Enforcement Rules:** `.vscode/copilot-mcp-rules.md`
- **Theme Guidelines:** `.vscode/copilot-instructions.md` (SfSkinManager section)
- **Approved Workflow:** `.vscode/approved-workflow.md`

---

*Last Updated: December 2025*
