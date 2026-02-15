# Custom Copilot Agents for Wiley Widget

This directory contains specialized GitHub Copilot agents tailored for the Wiley Widget Windows Forms application. These agents provide expert-level assistance for specific development tasks.

## Available Agents

### 1. Winnie - Windows Forms & Syncfusion Expert

**Agent File:** `Winnie.agent.md`

**Specialization:** Master-level Syncfusion Windows Forms control engineering

**Best For:**
- Creating new Windows Forms views with Syncfusion controls
- Refactoring existing views to MVVM patterns
- Implementing DockingManager layouts
- Theme management with SfSkinManager
- Syncfusion control configuration (SfDataGrid, RibbonControlAdv, Charts, etc.)
- UI/UX optimization within WinForms context

**Key Capabilities:**
- ✅ Enforces strict Syncfusion API documentation compliance
- ✅ Pure MVVM architecture (ViewModels, Commands, DataBinding)
- ✅ SfSkinManager theme consistency (Office2019Colorful default)
- ✅ DockingManager integration and panel management
- ✅ Production-ready view generation

**Example Usage:**
```
@Winnie Create a new DashboardView panel with SfDataGrid showing financial data, 
integrated with DockingManager, following our MainForm.Docking.cs patterns
```

```
@Winnie Refactor this view to pure MVVM with proper SfSkinManager theming
```

### 2. XPert - xUnit Testing Specialist

**Agent File:** `XPert.agent.md`

**Specialization:** Elite xUnit testing engineer for .NET 10+

**Best For:**
- Creating comprehensive xUnit test suites
- Unit testing Windows Forms ViewModels
- Integration testing with mocked dependencies
- Test coverage improvement
- Fixture and Theory implementations

**Key Capabilities:**
- ✅ xUnit v3 best practices (AAA pattern)
- ✅ Theories with InlineData/MemberData
- ✅ Proper mocking (Moq/NSubstitute)
- ✅ Test isolation and fixtures
- ✅ Coverage analysis integration

**Example Usage:**
```
@XPert Create comprehensive unit tests for the DashboardViewModel
```

```
@XPert Add integration tests for the QuickBooks sync service with proper mocking
```

### 3. GIT - CI/CD & GitHub Actions Expert

**Agent File:** `GIT.agent.md`

**Specialization:** GitHub Actions and CI/CD pipeline mastery

**Best For:**
- Workflow optimization
- CI/CD troubleshooting
- Pull request management
- Merge queue configuration
- Build pipeline improvements

**Key Capabilities:**
- ✅ Remote repository operations via GitHub MCP
- ✅ Workflow runs and logs analysis
- ✅ Merge queue and trunk-based development
- ✅ Security-hardened pipelines
- ✅ Performance optimization

**Example Usage:**
```
@GIT Analyze the failing CI pipeline for PR #42 and suggest fixes
```

```
@GIT Optimize our build workflow to reduce execution time
```

## How to Use These Agents

### In GitHub Copilot Chat (VS Code)

1. **Open Copilot Chat** (Ctrl+Alt+I or Cmd+Alt+I)

2. **Invoke an agent** by name with `@AgentName`:
   ```
   @Winnie Help me create a new invoice entry view with SfDataGrid
   ```

3. **The agent will**:
   - Use specialized knowledge for that domain
   - Access configured MCP tools (filesystem, Syncfusion Assistant, etc.)
   - Follow established patterns from MainForm and existing code
   - Provide production-ready solutions

### Agent Tool Access

All agents have access to:
- **MCP Filesystem Tools**: File reading, writing, searching (mandatory)
- **Syncfusion WinForms Assistant**: Official API documentation (via MCP)
- **Build & Test Tools**: `run_task`, `runTests` for validation
- **Error Analysis**: `get_errors` for diagnostics

### Best Practices

1. **Be Specific**: Provide context about existing code patterns
   ```
   @Winnie Following our MainForm.Docking.cs DockingManager pattern, 
   create a ReportsPanel with proper theme integration
   ```

2. **Reference Existing Code**: Agents understand the codebase
   ```
   @XPert Add tests similar to our existing ViewModel tests in 
   tests/WileyWidget.WinForms.Tests/ViewModels/
   ```

3. **Request Validation**: Ask agents to build/test after changes
   ```
   @Winnie Create the view and then validate with build task
   ```

4. **Leverage Expertise**: Use the right agent for the job
   - UI work → @Winnie
   - Tests → @XPert  
   - CI/CD → @GIT

## Integration with Existing Architecture

### Syncfusion DockingManager Integration

The Winnie agent is specifically trained on our DockingManager architecture:

- **MainForm.Docking.cs**: Centralized docking initialization
- **DockingHostFactory**: Panel creation patterns
- **SfSkinManager**: Single source of truth for theming
- **Panel Navigation**: Service-based navigation (`IPanelNavigationService`)

When creating new panels, Winnie will:
1. Follow existing `DockingHostFactory.CreateDockingHost` patterns
2. Apply proper `SfSkinManager.SetVisualStyle` theming
3. Integrate with `_dockingManager.DockControl` methods
4. Ensure proper Z-order and visibility management

### MVVM Patterns

All WinForms views created by Winnie follow:

- **ViewModels**: Implement `INotifyPropertyChanged` via `ObservableObject`
- **Commands**: Use `RelayCommand` from CommunityToolkit.Mvvm
- **DataBinding**: Only use declarative binding, no code-behind business logic
- **Dependency Injection**: Constructor injection for services and ViewModels

### Theme Consistency

Every control and panel will:
- Load theme assembly via `SkinManager.LoadAssembly()`
- Set `ThemeName` property to match application theme
- Inherit theme from parent controls via cascade
- Use `ThemeColors.ApplyTheme()` helper when appropriate

## Configuration Files

These agents are configured through:

- **Agent Definitions**: `.vscode/agents/*.agent.md` (this directory)
- **VS Code Settings**: `.vscode/settings.json` (MCP servers, Copilot config)
- **MCP Configuration**: Syncfusion Assistant, Filesystem, etc.

## Troubleshooting

### Agent Not Found

If `@AgentName` doesn't autocomplete:
1. Reload VS Code window (Ctrl+Shift+P → "Developer: Reload Window")
2. Check GitHub Copilot is enabled and authenticated
3. Verify agent files exist in `.vscode/agents/`

### Agent Can't Access MCP Tools

1. Check `.vscode/settings.json` has MCP servers configured
2. Ensure `github.copilot.chat.mcpServers` section includes:
   - `filesystem`
   - `syncfusion-winforms-assistant`
3. Restart VS Code after configuration changes

### Syncfusion Assistant Not Working

1. Set environment variable:
   ```powershell
   [System.Environment]::SetEnvironmentVariable('SYNCFUSION_API_KEY', 'your-key', 'User')
   ```
2. Run verification script:
   ```powershell
   .\scripts\verify-syncfusion-setup.ps1
   ```

## Additional Resources

- **Syncfusion Documentation**: https://help.syncfusion.com/windowsforms/overview
- **Main Documentation**: `docs/` directory
- **MCP Setup Guide**: `docs/MCP_SERVER_SETUP_GUIDE.md`
- **Project README**: Root `README.md`

## Contributing to Agents

When improving agent definitions:

1. Edit the `.md` file in `.vscode/agents/`
2. Keep the YAML frontmatter (description, tools)
3. Test changes by invoking the agent in Copilot Chat
4. Document new capabilities in this README
5. Commit changes following conventional commit guidelines

---

**Last Updated:** 2026-02-15
**Agent Versions:** Winnie v1.0 | XPert v1.0 | GIT v2.0
