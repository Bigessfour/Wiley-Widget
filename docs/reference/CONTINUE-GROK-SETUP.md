# Continue + Grok Integration Guide for Wiley Widget

## Overview
This guide helps you configure and test Continue with Grok for WinUI 3 development in VS Code.

## Configuration Files
- **Continue Config**: `.vscode/continue-config-xai.json`
- **VS Code Settings**: `.vscode/settings.json` (enhanced for WinUI 3 + Grok)

## Yellow Triangle Warning (⚠️)

### What It Means
The yellow triangle in Continue's plan/agent mode indicates potential compatibility issues. For Grok:
- Grok API is OpenAI-compatible but not officially supported by Continue
- Model names like "grok-beta" and "grok-4-0709" are flagged as experimental
- This is a **precautionary warning**, not a hard blocker

### Testing to "Prove Them Wrong"

#### Step 1: Basic Chat Test
1. Open a C# or XAML file in `src/WileyWidget.WinUI/`
2. Press `Ctrl+I` (or `Cmd+I` on macOS) to open Continue chat
3. Test queries:
   ```
   Explain this XAML x:Bind syntax
   Suggest LINQ optimization for this foreach loop
   How can I make this async method cancellable?
   ```
4. **Expected**: Accurate, context-aware responses

#### Step 2: Plan Mode Test
1. In Continue chat, ask a multi-step task:
   ```
   Plan a refactor to extract this UI code into a reusable UserControl:
   [paste XAML snippet]
   
   Steps should include:
   1. Create UserControl with DependencyProperties
   2. Move DataTemplates to Resources
   3. Update bindings to use x:Bind
   4. Add ViewModel backing
   ```
2. **Expected**: Clear step-by-step plan with rationale

#### Step 3: Agent Mode Test
1. Highlight code (e.g., a complex LINQ query)
2. Use slash commands:
   - `/edit` → "Optimize this LINQ query for performance"
   - `/test` → "Generate xUnit tests for this service"
   - `/winui-optimize` → "Apply WinUI 3 best practices"
3. **Expected**: Agent applies edits inline or suggests concrete changes

#### Step 4: Custom Commands Test
Try the project-specific commands:
- `/winui-optimize` → Uses your `.continue/rules/*.md` files
- `/xaml-fix` → Diagnoses XAML compilation issues
- `/csharp-async` → Reviews async/await patterns

### Suppress Warning (if tests pass)
Add to `.vscode/settings.json`:
```json
{
  "continue.telemetryLevel": "off",
  "continue.showExperimentalFeaturesWarning": false
}
```
Then reload VS Code: `Ctrl+Shift+P` → "Developer: Reload Window"

## Configuration Highlights

### Models
- **grok-4-0709**: Latest model for complex tasks (plan mode, refactoring)
- **grok-2-latest**: Stable model for production use
- **grok-beta**: Fast model for autocomplete and quick queries

### Custom Commands
1. **winui-optimize**: Applies Syncfusion MVVM + XAML optimization rules
2. **xaml-fix**: Diagnostic assistant for XAML compilation errors
3. **csharp-async**: Reviews async/await patterns (CS4014, fire-and-forget)

### Context Providers
- **codebase**: Retrieves 15 relevant files (optimized for WinUI project size)
- **file**: Includes `*.cs`, `*.xaml`, `*.csproj`, `*.json` files
- **diff**: Shows current changes for context-aware suggestions
- **terminal**: Reads terminal output for debugging help

## VS Code Settings Enhancements

### Performance Optimizations
- **Auto-save**: 1-second delay for fast iteration
- **Format on Save**: Applies `dotnet format` automatically
- **Minimap**: Disabled for faster XAML scrolling
- **File Watchers**: Excludes `bin/`, `obj/`, `*.nupkg` for speed

### .NET/WinUI Integration
- **IntelliSense**: Shows unimported namespaces, per-project references
- **Roslyn Analyzers**: Enabled for MVVM pattern detection
- **OmniSharp**: 60-second project load timeout (handles large WinUI solutions)
- **EditorConfig**: Enforces C# style rules (e.g., LINQ over loops)

### XAML Support
- **XML Formatting**: Preserves whitespace, no attribute splitting
- **Word Separators**: Optimized for XAML bindings (e.g., `Path=`, `Binding=`)

### Continue/Grok Settings
- **Default Model**: `grok-beta` (fast)
- **Chat Model**: `grok-4-0709` (complex tasks)
- **Max Tokens**: 4096 (handles full XAML files)
- **Temperature**: 0.7 (balanced creativity)
- **Experimental Features**: Enabled (plan mode, tools)
- **Telemetry**: Off (privacy)

## Testing Workflow

### 1. Initial Setup Verification
```powershell
# In VS Code terminal (Ctrl+`)
# Verify Continue extension installed
code --list-extensions | Select-String "continue"

# Check XAI API key loaded
$env:XAI_API_KEY
# Should output: xai-xxxxx...
```

### 2. Autocomplete Test
1. Open `src/WileyWidget.WinUI/ViewModels/MainViewModel.cs`
2. Type a comment: `// TODO: Add async method for`
3. Wait 1-2 seconds
4. **Expected**: Grok suggests method signature (e.g., `LoadDataAsync()`)

### 3. XAML Optimization Test
1. Open `src/WileyWidget.WinUI/Views/MainWindow.xaml`
2. Highlight a complex XAML block (e.g., nested `Grid` with many elements)
3. Use `/winui-optimize` command
4. **Expected**: Suggestions like:
   - Move inline styles to `<Page.Resources>`
   - Replace `Binding` with `x:Bind`
   - Extract `DataTemplate` for reuse

### 4. Error Diagnosis Test
1. Run XAML diagnostic task: `Ctrl+Shift+P` → "Tasks: Run Task" → "XAML: Diagnose Compilation Errors"
2. If errors found, paste into Continue chat
3. Ask: "Fix these XAML errors using WinUI 3 best practices"
4. **Expected**: Specific fixes (e.g., add namespace, correct resource key)

### 5. Plan Mode Full Test
Task: "Refactor MainViewModel to use CommunityToolkit.Mvvm source generators"
- **Expected Plan**:
  1. Add `[ObservableObject]` attribute to class
  2. Convert properties to `[ObservableProperty]` fields
  3. Replace `ICommand` with `[RelayCommand]` methods
  4. Update XAML bindings (no change needed with x:Bind)
  5. Remove manual `INotifyPropertyChanged` boilerplate

## Troubleshooting

### Issue: "API Key not found"
**Solution**: Add to `.env` file (root of workspace):
```env
XAI_API_KEY=xai-your-key-here
```
Then reload terminal: `Ctrl+Shift+P` → "Terminal: Kill All Terminals"

### Issue: "Model not responding"
**Solution**: Check API base URL in `continue-config-xai.json`:
```json
{
  "apiBase": "https://api.x.ai/v1"
}
```
Test connection:
```powershell
curl -H "Authorization: Bearer $env:XAI_API_KEY" https://api.x.ai/v1/models
```

### Issue: "Context too large"
**Solution**: Reduce `nRetrieve` in settings:
```json
{
  "continue.contextProviders.codebase.nRetrieve": 10  // was 15
}
```

### Issue: "Slow autocomplete"
**Solution**: Switch to faster model for tab completion:
```json
{
  "tabAutocompleteModel": {
    "model": "grok-beta"  // fastest option
  }
}
```

## Recommended Extensions

Install via `Ctrl+Shift+X`:
1. **C# Dev Kit** (ms-dotnettools.csdevkit)
2. **C#** (ms-dotnettools.csharp)
3. **Continue** (continuedev.continue) ← Already installed
4. **Error Lens** (usernamehw.errorlens) ← Inline error display
5. **GitLens** (eamodio.gitlens) ← Better codebase context

## Performance Benchmarks

### Expected Response Times (on typical dev machine)
- **Autocomplete**: 0.5-1.5 seconds
- **Simple query** ("Explain this code"): 2-4 seconds
- **Plan generation**: 5-10 seconds
- **Agent edit**: 3-6 seconds

### If slower than these:
1. Check internet connection (Grok API is remote)
2. Reduce context: Lower `nRetrieve` setting
3. Use `grok-beta` for faster responses
4. Close large files not in use

## Best Practices

### For WinUI 3 Development
1. **Always include XAML context**: Highlight relevant XAML when asking C# questions
2. **Use custom commands**: `/winui-optimize` applies project-specific rules
3. **Reference `.continue/rules/`**: Ask Grok to follow `syncfusion-winui-standards.md`
4. **Test incrementally**: Apply one suggestion at a time, rebuild between changes

### For Plan Mode
1. **Be specific**: "Refactor this to use async/await with CancellationToken"
2. **Provide constraints**: "Keep existing public API, only change implementation"
3. **Reference files**: "@MainViewModel.cs" to include full context

### For Agent Mode
1. **Highlight exact region**: Select the code block to edit
2. **Clear instructions**: "/edit Replace this foreach with LINQ"
3. **Review diffs**: Always check changes before applying

## Integration with Workflow

### Daily Workflow with Continue
1. **Morning health check**: Ask Grok to review latest CI failures
   ```
   @terminal Analyze this build error: [paste error]
   ```
2. **During development**: Use autocomplete for boilerplate
3. **Before commit**: Run `/xaml-fix` and `/csharp-async` on changed files
4. **Code review**: Ask Grok to explain complex LINQ or async patterns

### With MCP Tools
Continue integrates with your MCP setup:
- **C# MCP**: Grok can suggest `.csx` scripts for testing
- **Sequential Thinking MCP**: Complex refactors use step-by-step planning
- **Filesystem MCP**: Agent mode can read/write files with audit trail

## Reporting Issues

If Grok performance is excellent (90%+ success rate):
1. **Report to Continue**: https://github.com/continuedev/continue/issues
2. **Share config**: Attach `continue-config-xai.json`
3. **Include examples**: "Grok successfully refactored 15 ViewModels with zero errors"
4. **Advocate**: Ask Continue team to officially support xAI/Grok

## Next Steps

1. Run all 5 test steps above
2. Document results (success rate, response times)
3. If excellent: Suppress warning, use confidently
4. If issues: Report specific failures with examples
5. Customize `customCommands` for your specific patterns

---

**Configuration Version**: 1.0 (November 23, 2025)
**Tested with**: Continue v0.9.x, Grok API v1, WinUI 3 (.NET 9)
