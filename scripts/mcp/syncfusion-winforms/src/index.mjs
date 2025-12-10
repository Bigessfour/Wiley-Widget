import * as z from "zod/v4";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";

const server = new McpServer(
  { name: "syncfusion-winforms-mcp", version: "0.1.0" },
  { capabilities: { logging: {} } }
);

// In-repo, curated control catalog (seed, non-exhaustive)
import { controls } from "./controls.mjs";

// Optional: a resource exposing the catalog as JSON

// Minimal fetch tool: fetch docs by URL or control name
server.registerTool(
  "fetch-syncfusion-docs",
  {
    title: "Fetch Syncfusion WinForms Docs",
    description:
      "Fetch documentation content for a Syncfusion WinForms control. Provide a URL, or just the control name to fetch its overview page from the catalog.",
    inputSchema: {
      url: z.string().url().optional().describe("Direct docs URL to fetch"),
      control: z.string().optional().describe("Control name, e.g., SfDataGrid"),
      maxChars: z
        .number()
        .int()
        .positive()
        .max(200000)
        .optional()
        .describe("Max characters to return (default 8000)"),
    },
  },
  async ({ url, control, maxChars }) => {
    const limit = maxChars ?? 8000;
    let targetUrl = url;

    if (!targetUrl && control) {
      const match = controls.find((c) => c.name.toLowerCase() === String(control).toLowerCase());
      if (match?.docs) targetUrl = match.docs;
    }

    if (!targetUrl) {
      const msg = "Provide either a docs URL or a known control name.";
      return { content: [{ type: "text", text: msg }] };
    }

    try {
      const res = await fetch(targetUrl, { method: "GET" });
      if (!res.ok) {
        return { content: [{ type: "text", text: `HTTP ${res.status} fetching ${targetUrl}` }] };
      }
      const html = await res.text();
      const text = html
        .replace(/<script[\s\S]*?<\/script>/gi, "")
        .replace(/<style[\s\S]*?<\/style>/gi, "")
        .replace(/<[^>]+>/g, " ")
        .replace(/\s+/g, " ")
        .trim()
        .slice(0, limit);

      return {
        content: [
          { type: "text", text: `Source: ${targetUrl}` },
          { type: "text", text },
        ],
      };
    } catch (err) {
      return { content: [{ type: "text", text: `Fetch failed: ${String(err)}` }] };
    }
  }
);

// Register new control discovery / implementation tools
import { listControlsTool, implementControlTool } from "./tools/controls.mjs";

// Register tools (compatible with Model Context Protocol helpers)
server.registerTool(
  "list-syncfusion-controls",
  {
    title: listControlsTool.title,
    description: listControlsTool.description,
    inputSchema: listControlsTool.inputSchema,
  },
  listControlsTool.handler
);

server.registerTool(
  "implement-syncfusion-control",
  {
    title: implementControlTool.title,
    description: implementControlTool.description,
    inputSchema: implementControlTool.inputSchema,
  },
  implementControlTool.handler
);

// Connect over stdio
const transport = new StdioServerTransport();
await server.connect(transport);
