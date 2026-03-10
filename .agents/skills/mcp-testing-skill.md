# MCP Testing Skill — EvalCSharp UI Tests 🎯

Purpose

- Provide a concise, persistent reference for authoring and running WinForms UI/layout tests using the external Syncfusion MCP server.
- Clarify that **WinForms tests require a Windows host** and **do not** run in Linux containers (Docker).

When to use

- Any change that affects layout, theming, control hierarchy, docking, or Syncfusion controls requires an MCP EvalCSharp UI test.

Authoring pattern (recommended)

1. Create a `.csx` script that:
   - Instantiates the target `Control` or `Form` (use `new ..()` or `FormInstantiationHelper` where appropriate).
   - Sets a deterministic `Size` (e.g., `new Size(800, 600)`).
   - Calls `CreateControl()` and `PerformLayout()`.
   - Uses `TestHelper.Assert(condition, "message")` to assert layout invariants (header min height, grid fill, overlay bounds, etc.).
2. Add a small xUnit wrapper in `tools/WileyWidgetMcpServer/Tests` that calls:
   ```csharp
   var result = await EvalCSharpTool.EvalCSharp(csx, timeoutSeconds: 30, jsonOutput: true);
   var json = JsonDocument.Parse(result);
   Assert.True(json.RootElement.GetProperty("success").GetBoolean());
   ```

Run & validation

- Local: Run the MCP server on a **Windows host** using `scripts/tools/run-mcp.ps1` (recommended). Then run: `dotnet test tools/WileyWidgetMcpServer/WileyWidgetMcpServer.csproj` on that Windows host.
- CI: Rely on the external MCP server CI Windows runners (PRs that touch UI must include MCP tests; maintainers will run them on Windows).

Important rules ✅

- **MANDATORY**: UI/layout/theming changes must include an EvalCSharp test in `tools/WileyWidgetMcpServer/Tests` (external repo).
- **Do NOT** add WinForms UI/layout tests to the WileyWidget repository; the external MCP server is the canonical test layer.
- **Do NOT** expect tests to run successfully in Linux/Docker containers—these tests require Windows GDI.

Quick example (`.csx` snippet)

```
var panel = new ProactiveInsightsPanel();
panel.Size = new Size(800, 600);
panel.CreateControl();
panel.PerformLayout();
TestHelper.Assert(panel.Controls.Find("ProactiveTopPanel", true).Length == 1, "Top panel missing");
return true;
```

Notes

- Keep tests small and focused; prefer deterministic layout assertions (bounds, size, ordering) rather than visual pixel checks.
- Add a short note to the PR describing the intent and the MCP test path so reviewers can run/validate easily.

---

_Last updated: 2026-01-10_
