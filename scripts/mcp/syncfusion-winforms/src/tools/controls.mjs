import * as z from 'zod/v4';
import { controls, getControlByName } from '../controls.mjs';

// Utility: generate C# sample implementation for a control
function sampleImplementation(control) {
  switch ((control?.name || '').toLowerCase()) {
    case 'sfdatagrid':
      return `// Sample SfDataGrid usage - production-ready example (C# WinForms)
// Requires: Syncfusion.WinForms.DataGrid

using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.DataGrid.Enums;
using System.Collections.Generic;

public class Product {
  public int Id { get; set; }
  public string Name { get; set; }
  public decimal Price { get; set; }
}

// In your form initialization
private void InitializeGrid() {
  var grid = new SfDataGrid();
  grid.Dock = DockStyle.Fill;
  grid.AutoGenerateColumns = false;

  grid.Columns.Add(new GridTextColumn() { MappingName = "Id", HeaderText = "ID", AllowEditing = false });
  grid.Columns.Add(new GridTextColumn() { MappingName = "Name", HeaderText = "Name", AllowEditing = true });
  grid.Columns.Add(new GridNumericColumn() { MappingName = "Price", HeaderText = "Price", Format = "C" });

  // Enable sorting and filtering
  grid.AllowSorting = true;
  grid.AllowFiltering = true;

  // High-performance mode for large datasets
  grid.EnableVirtualization = true;

  // Bind to a strongly-typed list
  List<Product> products = GetProducts();
  grid.DataSource = products;
  this.Controls.Add(grid);
}
`;
    case 'sfchart':
      return `// Sample SfChart usage - production-ready C# WinForms example
// Requires: Syncfusion.WinForms.Forms and Syncfusion.WinForms.Chart

using Syncfusion.WinForms.Chart;
using System.Collections.Generic;

private void InitializeChart() {
  var chart = new SfCartesianChart();
  chart.Dock = DockStyle.Fill;

  var series = new LineSeries() {
    DataSource = new List<object>() {
      new { X = 1, Y = 10 },
      new { X = 2, Y = 20 },
      new { X = 3, Y = 15 },
    },
    XBindingPath = "X",
    YBindingPath = "Y",
    Label = "Sales"
  };

  chart.Series.Add(series);
  this.Controls.Add(chart);
}
`;
    case 'sfcombobox':
      return `// Sample SfComboBox usage - production-ready C# WinForms
// Requires: Syncfusion.WinForms.ListView

using Syncfusion.WinForms.ListView;
using System.Collections.Generic;

private void InitializeComboBox() {
  var combo = new SfComboBox();
  combo.Dock = DockStyle.Top;
  combo.DropDownStyle = ComboBoxStyle.DropDownList;

  combo.DataSource = new List<string>() { "Option A", "Option B", "Option C" };
  combo.ValueMember = null; // simple list

  // Best pract: handle SelectedIndexChanged for user interactions
  combo.SelectedIndexChanged += (s, e) => {
    var v = combo.SelectedItem;
    // handle value
  };

  this.Controls.Add(combo);
}
`;
    case 'sftabcontrol':
      return `// Sample SfTabControl usage - C# WinForms production-ready
// Requires: Syncfusion.WinForms.Tools

using Syncfusion.Windows.Forms.Tools;

private void InitializeTabControl() {
  var tabs = new Syncfusion.Windows.Forms.Tools.TabControlAdv();
  tabs.Dock = DockStyle.Fill;

  var page1 = new TabPageAdv() { Text = "Main" };
  var page2 = new TabPageAdv() { Text = "Settings" };

  page1.Controls.Add(new Label() { Text = "Main content", Dock = DockStyle.Fill });
  page2.Controls.Add(new Label() { Text = "Settings content", Dock = DockStyle.Fill });

  tabs.TabPages.Add(page1);
  tabs.TabPages.Add(page2);
  this.Controls.Add(tabs);
}
`;
    default:
      return `// No prebuilt sample for ${control?.name || 'unknown'}. Please provide additional details or request a tailored implementation.`;
  }
}

export const listControlsTool = {
  title: 'List Syncfusion WinForms Controls',
  description: 'Return a structured list of curated Syncfusion WinForms controls available for implementation',
  inputSchema: {},
  handler: async (_input) => {
    const items = controls.map(c => ({ name: c.name, category: c.category, description: c.description, docs: c.docs }));
    return { content: [{ type: 'json', data: items }] };
  }
};

export const implementControlTool = {
  title: 'Implement Syncfusion WinForms Control',
  description: 'Generate production-ready implementation guidance and sample code for a Syncfusion WinForms control (or all controls).',
  inputSchema: {
    control: z.string().optional().describe('Control name, e.g., SfDataGrid - omit to target all'),
    all: z.boolean().optional().describe('If true, generate implementations for all known controls'),
    targetLanguage: z.string().optional().describe('Language/format for implementation, default: C# WinForms')
  },
  handler: async ({ control, all, targetLanguage } = {}) => {
    const lang = targetLanguage ?? 'C# WinForms';

    if (all) {
      // Generate implementations for all controls
      const results = controls.map(c => {
        return {
          name: c.name,
          category: c.category,
          docs: c.docs,
          description: c.description,
          implementation: sampleImplementation(c),
          notes: `Production-ready tips: ensure Syncfusion NuGet packages are installed, initialize license at program startup per Syncfusion licensing docs, add proper error handling and data-validation in real apps.`
        };
      });
      return { content: [{ type: 'json', data: results }] };
    }

    if (!control) {
      return { content: [{ type: 'text', text: 'Provide a control name or set "all" to true.' }] };
    }

    const match = getControlByName(control);
    if (!match) {
      return { content: [{ type: 'text', text: `Control '${control}' not found in the curated catalog.` }] };
    }

    return {
      content: [
        { type: 'text', text: `Control: ${match.name} — ${match.description}` },
        { type: 'text', text: `Docs: ${match.docs}` },
        { type: 'code', language: lang, text: sampleImplementation(match) },
        { type: 'text', text: 'Notes: Production-ready tips: ensure Syncfusion NuGet packages are installed and licensed, use data-binding patterns, add input validation, and wrap UI updates on the UI thread.' }
      ]
    };
  }
};