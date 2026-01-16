# Wiley Widget MCP "Sister Companion" Tests

This directory contains headless validation scripts run by the **WileyWidgetMcpServer**. These tests ensure UI consistency, stability, and configuration integrity without launching the full GUI.

## Directory Structure

- **`Audits/`**: Static analysis scripts that check for theme compliance, binding errors, and configuration risks.
- **`GoldenMasters/`**: (Future) JSON snapshots of control hierarchies for regression testing.
- **`Integration/`**: Scripts that exercise complex interactions (e.g., Panel navigation).
- **`Snapshots/`**: Scripts to generate Golden Masters.

## How to Run Tests

You can run these scripts using any MCP Client (Copilot, Claude Desktop) connected to the `WileyWidgetMcpServer`.

### Using Copilot Chat

Ask Copilot:

> "Use RunHeadlessFormTestTool to run the script 'tests/WileyWidget.McpTests/Audits/ThemeCompliance.csx'"

### Available Scripts

#### 1. Theme Compliance Audit

**Script:** `Audits/ThemeCompliance.csx`
**Checks:**

- Scans all Forms in `WileyWidget.WinForms`.
- Detects manual `BackColor` / `ForeColor` assignments that break theming.
- Ignored semantic colors (Red/Green/Orange).

#### 2. DataGrid Configuration Audit

**Script:** `Audits/GridConfiguration.csx`
**Checks:**

- Instantiates forms.
- Finds `SfDataGrid` controls.
- Verifies Columns generally have valid `MappingNames`.
- _(Note: Requires parameterless constructors or simple MainForm dependency)_

## Adding New Tests

1. Create a `.csx` file in `Audits/` or `Integration/`.
2. Use standard C# code.
3. Access `WileyWidget.WinForms` types directly.
4. Return a `string` (log/report) at the end of the script.
