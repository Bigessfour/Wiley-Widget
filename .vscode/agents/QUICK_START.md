# Quick Start Guide: Custom Copilot Agents

## TL;DR - Using Agents in 30 Seconds

1. **Open Copilot Chat** in VS Code (Ctrl+Alt+I)
2. **Type `@` and select an agent**:
   - `@Winnie` - Windows Forms & Syncfusion expert
   - `@XPert` - xUnit testing specialist
   - `@GIT` - CI/CD & GitHub Actions expert
3. **Ask your question**
4. **Agent responds with specialized expertise**

## Common Scenarios

### Creating a New Windows Forms Panel

```
@Winnie I need to create a new InvoiceListPanel with the following requirements:
- SfDataGrid showing invoice data (InvoiceId, CustomerName, Amount, Date)
- Integrated with DockingManager like other panels in MainForm.Docking.cs
- Bound to InvoiceListViewModel with proper MVVM
- Office2019Colorful theme via SfSkinManager
- Commands for Add/Edit/Delete operations
```

### Adding Tests for a ViewModel

```
@XPert Create comprehensive unit tests for DashboardViewModel including:
- Property change notifications
- Command execution and CanExecute logic
- Mock repository interactions
- Edge cases and error handling
```

### Debugging CI Pipeline

```
@GIT The build workflow is failing on PR #15. Can you:
1. Analyze the workflow run logs
2. Identify the root cause
3. Suggest a fix for the workflow YAML
```

## Agent Capabilities Quick Reference

| Task | Agent | Example |
|------|-------|---------|
| New WinForms view | @Winnie | `@Winnie Create SettingsPanel with TreeView` |
| Refactor to MVVM | @Winnie | `@Winnie Convert this code-behind to pure MVVM` |
| Fix theme issues | @Winnie | `@Winnie Why isn't the theme applying to this control?` |
| DockingManager setup | @Winnie | `@Winnie Add this panel to DockingManager` |
| Write unit tests | @XPert | `@XPert Test this ViewModel with Moq` |
| Integration tests | @XPert | `@XPert Create DB integration tests` |
| Fix failing tests | @XPert | `@XPert Why is this test flaky?` |
| Workflow debugging | @GIT | `@GIT What's wrong with our CI?` |
| PR management | @GIT | `@GIT List open PRs ready to merge` |
| Pipeline optimization | @GIT | `@GIT Speed up our build time` |

## Best Practices

### ✅ Do This

- **Be specific about context**: "Following our MainForm.Docking.cs pattern..."
- **Reference existing code**: "Similar to DashboardPanel..."
- **Request validation**: "Please build and test the changes"
- **Ask for explanations**: "Explain why you chose this approach"

### ❌ Avoid This

- Vague requests: "Make a panel"
- Mixing concerns: "Create a view and set up CI" (use separate agents)
- Skipping context: Agents work better with background info
- Ignoring agent expertise: Use the right agent for the job

## Multi-Agent Workflows

Complex tasks can involve multiple agents:

### Example: Adding a Complete Feature

1. **Design Phase** (You or @Winnie)
   ```
   @Winnie Propose a design for a ReportingPanel with charts and grids
   ```

2. **Implementation** (@Winnie)
   ```
   @Winnie Implement the ReportingPanel and ReportingViewModel
   ```

3. **Testing** (@XPert)
   ```
   @XPert Create tests for ReportingViewModel
   ```

4. **CI Integration** (@GIT)
   ```
   @GIT Ensure the new tests run in our CI pipeline
   ```

## Configuration Check

Verify your environment is set up correctly:

### 1. Check MCP Servers

Look in `.vscode/settings.json` for:
```json
"github.copilot.chat.mcpServers": {
  "filesystem": { ... },
  "syncfusion-winforms-assistant": { ... }
}
```

### 2. Check Agent Files

Ensure files exist:
- `.vscode/agents/Winnie.agent.md`
- `.vscode/agents/XPert.agent.md`
- `.vscode/agents/GIT.agent.md`

### 3. Test an Agent

Try this simple test:
```
@Winnie What Syncfusion controls are used in MainForm.Docking.cs?
```

If the agent responds with accurate information about DockingManager, you're good to go!

## Troubleshooting

### "Agent not found" error

**Solution:** Reload VS Code
- Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
- Type "Developer: Reload Window"
- Press Enter

### Syncfusion Assistant errors

**Solution:** Set API key environment variable
```powershell
# PowerShell
[System.Environment]::SetEnvironmentVariable('SYNCFUSION_API_KEY', 'your-key-here', 'User')
```

Then restart VS Code.

### Agent gives generic responses

**Possible causes:**
1. Agent definition file may be corrupted - check `.vscode/agents/AgentName.agent.md`
2. MCP tools not configured - verify `settings.json`
3. Need more context - provide specific details about your codebase

## Getting Help

- **Full Documentation**: See `README.md` in this directory
- **MCP Setup**: See `docs/MCP_SERVER_SETUP_GUIDE.md`
- **Project Docs**: See `docs/` directory
- **Syncfusion Docs**: https://help.syncfusion.com/windowsforms

---

**Tip:** Agents learn from your codebase. The more context you provide, the better the responses!
