import * as z from 'zod/v4';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';

const server = new McpServer(
  { name: 'syncfusion-winforms-mcp', version: '0.1.0' },
  { capabilities: { logging: {} } }
);

// In-repo, curated control catalog (seed, non-exhaustive)
const controls = [
  {
    name: 'SfDataGrid',
    category: 'DataGrid',
    description: 'High-performance data grid for WinForms with sorting, grouping, and filtering.',
    docs: 'https://help.syncfusion.com/windowsforms/datagrid/overview'
  },
  {
    name: 'SfChart',
    category: 'Chart',
    description: 'Comprehensive chart control for WinForms with multiple series and axes.',
    docs: 'https://help.syncfusion.com/windowsforms/chart/overview'
  },
  {
    name: 'SfComboBox',
    category: 'Editors',
    description: 'Combo box editor with filtering and data-binding support.',
    docs: 'https://help.syncfusion.com/windowsforms/combobox/overview'
  },
  {
    name: 'SfTabControl',
    category: 'Navigation',
    description: 'Tabbed navigation control with themes and drag/drop.',
    docs: 'https://help.syncfusion.com/windowsforms/tabcontrol/overview'
  }
];




// Optional: a resource exposing the catalog as JSON

// Minimal fetch tool: fetch docs by URL or control name
server.registerTool(
  'fetch-syncfusion-docs',
  {
    title: 'Fetch Syncfusion WinForms Docs',
    description: 'Fetch documentation content for a Syncfusion WinForms control. Provide a URL, or just the control name to fetch its overview page from the catalog.',
    inputSchema: {
      url: z.string().url().optional().describe('Direct docs URL to fetch'),
      control: z.string().optional().describe('Control name, e.g., SfDataGrid'),
      maxChars: z.number().int().positive().max(200000).optional().describe('Max characters to return (default 8000)')
    }
  },
  async ({ url, control, maxChars }) => {
    const limit = maxChars ?? 8000;
    let targetUrl = url;

    if (!targetUrl && control) {
      const match = controls.find(c => c.name.toLowerCase() === String(control).toLowerCase());
      if (match?.docs) targetUrl = match.docs;
    }

    if (!targetUrl) {
      const msg = 'Provide either a docs URL or a known control name.';
      return { content: [{ type: 'text', text: msg }] };
    }

    try {
      const res = await fetch(targetUrl, { method: 'GET' });
      if (!res.ok) {
        return { content: [{ type: 'text', text: `HTTP ${res.status} fetching ${targetUrl}` }] };
      }
      const html = await res.text();
      const text = html
        .replace(/<script[\s\S]*?<\/script>/gi, '')
        .replace(/<style[\s\S]*?<\/style>/gi, '')
        .replace(/<[^>]+>/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, limit);

      return {
        content: [
          { type: 'text', text: `Source: ${targetUrl}` },
          { type: 'text', text }
        ]
      };
    } catch (err) {
      return { content: [{ type: 'text', text: `Fetch failed: ${String(err)}` }] };
    }
  }
);

// Connect over stdio
const transport = new StdioServerTransport();
await server.connect(transport);
