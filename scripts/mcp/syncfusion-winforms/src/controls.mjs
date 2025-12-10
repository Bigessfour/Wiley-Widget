export const controls = [
  {
    name: "SfDataGrid",
    category: "DataGrid",
    description: "High-performance data grid for WinForms with sorting, grouping, and filtering.",
    docs: "https://help.syncfusion.com/windowsforms/datagrid/overview",
  },
  {
    name: "SfChart",
    category: "Chart",
    description: "Comprehensive chart control for WinForms with multiple series and axes.",
    docs: "https://help.syncfusion.com/windowsforms/chart/overview",
  },
  {
    name: "SfComboBox",
    category: "Editors",
    description: "Combo box editor with filtering and data-binding support.",
    docs: "https://help.syncfusion.com/windowsforms/combobox/overview",
  },
  {
    name: "SfTabControl",
    category: "Navigation",
    description: "Tabbed navigation control with themes and drag/drop.",
    docs: "https://help.syncfusion.com/windowsforms/tabcontrol/overview",
  },
];

export function getControlByName(name) {
  return controls.find((c) => c.name.toLowerCase() === String(name).toLowerCase());
}
