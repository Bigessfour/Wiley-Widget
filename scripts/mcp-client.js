#!/usr/bin/env node

/**
 * Lightweight MCP client shim for CI automation.
 * Provides minimal support for registering tools via manifest verification
 * and invoking MCP tool endpoints exposed by the local HTTP server.
 */

import { readFileSync } from "fs";
import { resolve } from "path";

function printUsage(message) {
  if (message) {
    console.error(message);
  }
  console.error("Usage: node scripts/mcp-client.js client <command> [options]\n" +
    "Commands:\n" +
    "  register-tool --url <url> --manifest <path>\n" +
    "  call --url <url> --tool-id <id> [--params <json>]");
}

function parseArgs(args) {
  const options = {};
  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (!arg.startsWith("--")) {
      continue;
    }

    const eqIndex = arg.indexOf("=");
    if (eqIndex > -1) {
      const key = arg.slice(2, eqIndex);
      options[key] = arg.slice(eqIndex + 1);
      continue;
    }

    const key = arg.slice(2);
    const next = args[i + 1];
    if (next && !next.startsWith("--")) {
      options[key] = next;
      i += 1;
    } else {
      options[key] = true;
    }
  }
  return options;
}

function ensureUrl(url) {
  if (!url) {
    throw new Error("Missing required --url argument");
  }
  return url.endsWith("/") ? url.slice(0, -1) : url;
}

async function registerTools(options) {
  const baseUrl = ensureUrl(options.url);
  if (!options.manifest) {
    throw new Error("Missing required --manifest argument");
  }

  const manifestPath = resolve(process.cwd(), options.manifest);
  const manifestContent = readFileSync(manifestPath, "utf8");
  const manifest = JSON.parse(manifestContent);

  const response = await fetch(`${baseUrl}/health`);
  if (!response.ok) {
    throw new Error(`Health check failed with status ${response.status}`);
  }

  const health = await response.json();
  const manifestIds = (manifest.tools || []).map((tool) => tool.id);
  const serverNames = (health.tools || []).map((tool) => tool.name);
  const missing = manifestIds.filter((id) => !serverNames.includes(id));

  if (missing.length > 0) {
    throw new Error(`Server missing tools: ${missing.join(", ")}`);
  }

  console.log(`MCP server ready at ${baseUrl}. Registered tools: ${serverNames.join(", ")}`);
}

async function callTool(options) {
  const baseUrl = ensureUrl(options.url);
  const toolId = options["tool-id"];
  if (!toolId) {
    throw new Error("Missing required --tool-id argument");
  }

  let params = {};
  if (options.params) {
    try {
      params = JSON.parse(options.params);
    } catch (error) {
      throw new Error(`Invalid JSON for --params: ${error.message}`);
    }
  }

  const response = await fetch(`${baseUrl}/tools/${toolId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(params),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Tool call failed with status ${response.status}: ${body}`);
  }

  const payload = await response.json();
  console.log(JSON.stringify(payload, null, 2));
}

async function main() {
  const [, , command, subcommand, ...rest] = process.argv;
  if (command !== "client") {
    printUsage("Unknown command. Expected 'client'.");
    process.exitCode = 1;
    return;
  }

  if (!subcommand) {
    printUsage("Missing subcommand.");
    process.exitCode = 1;
    return;
  }

  const options = parseArgs(rest);
  try {
    if (subcommand === "register-tool") {
      await registerTools(options);
    } else if (subcommand === "call") {
      await callTool(options);
    } else {
      printUsage(`Unknown subcommand '${subcommand}'.`);
      process.exitCode = 1;
    }
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}

await main();
