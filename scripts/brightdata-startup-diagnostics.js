// BrightData Startup Diagnostics & Unified Search Aggregator
// Modes: REST (default) or MCP (experimental). Provides per-query results and an aggregated
// de-duplicated corpus with heuristic relevance scoring.
// Inspired by Bright Data unified-search agent reference implementation (external repo).

// --- Fetch Bootstrap (prefer global fetch or undici) ---
let fetchFn = global.fetch;
if (!fetchFn) {
  try {
    fetchFn = require("undici").fetch;
  } catch {
    /* ignore */
  }
}
if (!fetchFn) {
  console.error(
    "[Diagnostics] No fetch implementation (need Node 18+ or undici).",
  );
  process.exit(1);
}

// --- Env / Config ---
// Optional lightweight .env loader (only sets vars not already present)
try {
  const fs = require("fs");
  if (fs.existsSync(".env")) {
    const lines = fs.readFileSync(".env", "utf8").split(/\r?\n/);
    for (const line of lines) {
      if (!line || line.trim().startsWith("#")) continue;
      const m = line.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
      if (m) {
        const key = m[1];
        if (!(key in process.env)) {
          let val = m[2].trim();
          // Strip wrapping quotes if present
          if (
            (val.startsWith('"') && val.endsWith('"')) ||
            (val.startsWith("'") && val.endsWith("'"))
          ) {
            val = val.slice(1, -1);
          }
          process.env[key] = val;
        }
      }
    }
  }
} catch {
  /* ignore .env errors */
}
const API_KEY = process.env.BRIGHTDATA_API_KEY || process.env.API_TOKEN;
// Allow overriding the raw REST endpoint to experiment if 404 occurs due to path mismatch.
// Authentication per official docs: https://docs.brightdata.com/api-reference/authentication
// Default header: Authorization: Bearer <API_KEY>. Optional x-api-key only if a product path expects it.
// Known endpoint possibilities (product-specific; may evolve):
//  - https://api.brightdata.com/search (legacy/unified?)
//  - https://api.brightdata.com/ai-tools/search
//  - https://api.brightdata.com/unified-search
//  - https://api.brightdata.com/request (generic request endpoint for some products)
//  - (MCP / HOSTED modes may bypass direct REST entirely)
const API_ENDPOINT =
  process.env.BRIGHTDATA_ENDPOINT || "https://api.brightdata.com/search";
const AUTODETECT = process.env.BRIGHTDATA_AUTODETECT === "1";
if (!API_KEY) {
  console.error(
    "[Diagnostics] Missing BRIGHTDATA_API_KEY env var. Get one at https://brightdata.com/cp",
  );
  process.exit(1);
}
// MODE options:
//  REST    -> direct HTTPS POST
//  MCP     -> local JSON-RPC tool provider (@brightdata/mcp by default)
//  HOSTED  -> Bright Data hosted MCP (adds --hosted automatically)
//  AUTO    -> Try MCP first; on per-query failure fallback transparently to REST
let MODE = (process.env.BRIGHTDATA_MODE || "REST").toUpperCase();
const MAX_PER_QUERY = Number(process.env.BRIGHTDATA_LIMIT || 5);
const OUTPUT_JSON = process.env.BRIGHTDATA_OUTPUT_JSON; // optional aggregated JSON output path
const SUMMARY = process.env.BRIGHTDATA_SUMMARY === "1"; // produce heuristic summary

// Core diagnostic queries (tunable via env override)
const QUERIES = (process.env.BRIGHTDATA_QUERIES || "")
  .split("|")
  .filter(Boolean);
if (!QUERIES.length) {
  QUERIES.push(
    "WPF application exits immediately exit code -1",
    "Syncfusion WPF early startup crash license",
    "WPF App constructor not hit debug.log only module initializer",
    "WPF exit code 0xffffffff startup resources xaml",
    "Syncfusion SfSkinManager early license registration requirement",
  );
}

// --- REST Search ---
async function searchRest(query) {
  const autoHeader = process.env.BRIGHTDATA_AUTO_HEADER !== "0";
  const forceX = process.env.BRIGHTDATA_USE_X_HEADER === "1";
  const attemptOrder = forceX ? ["x"] : ["bearer", "x"];
  let lastErr = null;
  for (let i = 0; i < attemptOrder.length; i++) {
    const scheme = attemptOrder[i];
    if (scheme === "x" && !autoHeader && !forceX) continue; // skip implicit x-api-key attempt if auto disabled
    let res;
    let bodyText = null;
    try {
      res = await fetchFn(API_ENDPOINT, {
        method: "POST",
        headers:
          scheme === "x"
            ? { "x-api-key": API_KEY, "Content-Type": "application/json" }
            : {
                Authorization: `Bearer ${API_KEY}`,
                "Content-Type": "application/json",
              },
        body: JSON.stringify({
          query,
          limit: MAX_PER_QUERY,
          country: "US",
          language: "en",
        }),
      });
    } catch (networkErr) {
      return { error: `Network error: ${networkErr.message}` };
    }
    if (!res.ok) bodyText = await res.text().catch(() => "");
    // Success path
    if (res.ok) {
      try {
        const json = await res.json();
        if (!json.results || !json.results.length)
          return { error: "No results" };
        const mapped = json.results.slice(0, MAX_PER_QUERY).map((r) => ({
          title: r.title,
          url: r.url,
          snippet: r.snippet,
          rank: r.rank,
        }));
        if (scheme === "x" && !forceX)
          mapped.headerFallback = "used_x_api_key_after_bearer_failure";
        return mapped;
      } catch {
        return { error: "Parse failure" };
      }
    }
    // Evaluate if we should retry with x-api-key
    const status = res.status;
    const snippet = bodyText?.slice(0, 200) || "no body";
    if (
      scheme === "bearer" &&
      autoHeader &&
      !forceX &&
      (status === 401 || status === 403)
    ) {
      lastErr = `Bearer auth failed (${status}) ${snippet}`;
      continue; // try x-api-key
    }
    if (status === 404) {
      return {
        error: `HTTP 404 – Endpoint not found. Tried ${API_ENDPOINT}. Body: ${snippet}`,
      };
    }
    // Terminal failure for this scheme
    return { error: `HTTP ${status} – ${snippet}` };
  }
  return { error: lastErr || "Unknown auth negotiation failure" };
}

// --- MCP Configuration Check ---
async function checkMcpConfig() {
  console.log("🔧 Checking MCP configuration and connection...");
  const DEBUG = true; // Force debug for config check
  let spawn;
  try {
    spawn = require("child_process").spawn;
  } catch {
    return { error: "child_process unavailable" };
  }

  let mcpCmd = process.env.BRIGHTDATA_MCP_CMD || "node";
  let mcpArgs = process.env.BRIGHTDATA_MCP_ARGS
    ? JSON.parse(process.env.BRIGHTDATA_MCP_ARGS)
    : ["node_modules/@brightdata/mcp/server.js"];
  const wantsHosted =
    MODE === "HOSTED" || process.env.BRIGHTDATA_MCP_HOSTED === "1";
  if (wantsHosted && !mcpArgs.includes("--hosted"))
    mcpArgs = [...mcpArgs, "--hosted"];

  const env = { ...process.env };
  if (!env.API_TOKEN && API_KEY) env.API_TOKEN = API_KEY;

  let proc;
  try {
    proc = spawn(mcpCmd, mcpArgs, { env, stdio: ["pipe", "pipe", "pipe"] });
  } catch (e) {
    return { error: `Failed to spawn MCP: ${e.message}` };
  }

  const stderrBuf = [];
  proc.stderr.on("data", (d) => {
    const t = d.toString();
    stderrBuf.push(t);
    console.log("[CONFIG CHECK STDERR]", t.trim());
  });

  let buffer = "";
  const messages = [];
  const listeners = [];
  function onMessage(cb) {
    listeners.push(cb);
  }

  proc.stdout.on("data", (chunk) => {
    buffer += chunk.toString();
    // Parse newline-delimited JSON messages
    const lines = buffer.split("\n");
    buffer = lines.pop() || ""; // Keep the incomplete line

    for (const line of lines) {
      if (!line.trim()) continue;
      let msg;
      try {
        msg = JSON.parse(line.trim());
      } catch {
        console.error("[CONFIG] JSON parse error:", line.slice(0, 100));
        continue;
      }
      messages.push(msg);
      console.log("[CONFIG <=]", JSON.stringify(msg, null, 2));
      listeners.forEach((l) => {
        try {
          l(msg);
        } catch {}
      });
    }
  });

  function send(msg) {
    const json = JSON.stringify(msg);
    console.log("[CONFIG =>]", json);
    // MCP uses simple newline-delimited JSON, not HTTP-style Content-Length
    proc.stdin.write(json + "\n");
  }

  return new Promise((resolve) => {
    const timeout = setTimeout(() => {
      console.log("⏰ Configuration check timeout");
      try {
        proc.kill();
      } catch {}
      resolve({
        status: "timeout",
        stderr: stderrBuf.join(""),
        messages: messages.length,
        recommendation:
          "Connection established but timed out during tool discovery",
      });
    }, 10000);

    let toolsReceived = false;
    onMessage((msg) => {
      if (msg.id === 1 && msg.result && msg.result.tools) {
        toolsReceived = true;
        const tools = msg.result.tools.map((t) => ({
          name: t.name,
          description: t.description,
        }));
        clearTimeout(timeout);
        try {
          proc.kill();
        } catch {}
        resolve({
          status: "success",
          tools: tools,
          stderr: stderrBuf.join(""),
          messages: messages.length,
          recommendation:
            "MCP server is working perfectly! Fixed JSON-RPC framing resolved the connection issues.",
        });
      }
    });

    proc.on("exit", (code) => {
      if (!toolsReceived) {
        clearTimeout(timeout);
        resolve({
          status: "failed",
          exitCode: code,
          stderr: stderrBuf.join(""),
          messages: messages.length,
          recommendation:
            "MCP server exited before tool discovery. Check API key and network.",
        });
      }
    });

    // Start configuration check
    send({ jsonrpc: "2.0", id: 1, method: "tools/list" });
  });
}

// --- MCP Search (Content-Length framed JSON-RPC) ---
async function searchMcp(query) {
  const DEBUG = process.env.BRIGHTDATA_MCP_DEBUG === "1";
  let spawn;
  try {
    spawn = require("child_process").spawn;
  } catch {
    return { error: "child_process unavailable" };
  }
  let mcpCmd = process.env.BRIGHTDATA_MCP_CMD || "npx";
  let mcpArgs = process.env.BRIGHTDATA_MCP_ARGS
    ? JSON.parse(process.env.BRIGHTDATA_MCP_ARGS)
    : ["@brightdata/mcp"];
  const wantsHosted =
    MODE === "HOSTED" || process.env.BRIGHTDATA_MCP_HOSTED === "1";
  if (wantsHosted && !mcpArgs.includes("--hosted"))
    mcpArgs = [...mcpArgs, "--hosted"];
  // Early dependency presence check so AUTO mode can fallback cleanly
  let modulePresent = true;
  try {
    require.resolve("@brightdata/mcp/package.json");
  } catch {
    modulePresent = false;
  }
  if (!modulePresent && mcpCmd === "npx" && !process.env.BRIGHTDATA_MCP_CMD) {
    return {
      error:
        "MCP module @brightdata/mcp not installed. Run: npm install @brightdata/mcp (fallback to REST).",
    };
  }
  const env = { ...process.env };
  if (!env.API_TOKEN && API_KEY) env.API_TOKEN = API_KEY;

  let proc;
  try {
    proc = spawn(mcpCmd, mcpArgs, { env, stdio: ["pipe", "pipe", "pipe"] });
  } catch (e) {
    if (mcpCmd === "npx") {
      // Fallback: direct node execution if npx missing
      try {
        const path = require("path");
        const fs = require("fs");
        const candidate = path.join(
          process.cwd(),
          "node_modules",
          "@brightdata",
          "mcp",
          "dist",
          "index.js",
        );
        if (fs.existsSync(candidate)) {
          mcpCmd = "node";
          mcpArgs = [
            candidate,
            ...(wantsHosted && !mcpArgs.includes("--hosted")
              ? ["--hosted"]
              : []),
          ];
          proc = spawn(mcpCmd, mcpArgs, {
            env,
            stdio: ["pipe", "pipe", "pipe"],
          });
        } else {
          return {
            error: `Failed to spawn MCP (npx missing) and module not found: ${e.message}`,
          };
        }
      } catch (inner) {
        return { error: `Failed MCP spawn fallback: ${inner.message}` };
      }
    } else {
      return { error: `Failed to spawn MCP: ${e.message}` };
    }
  }

  const stderrBuf = [];
  proc.stderr.on("data", (d) => {
    const t = d.toString();
    stderrBuf.push(t);
    if (DEBUG) process.stderr.write("[MCP STDERR] " + t);
  });

  let buffer = "";
  const messages = [];
  const listeners = [];
  function onMessage(cb) {
    listeners.push(cb);
  }

  proc.stdout.on("data", (chunk) => {
    buffer += chunk.toString();
    // Parse newline-delimited JSON messages
    const lines = buffer.split("\n");
    buffer = lines.pop() || ""; // Keep the incomplete line

    for (const line of lines) {
      if (!line.trim()) continue;
      let msg;
      try {
        msg = JSON.parse(line.trim());
      } catch {
        if (DEBUG) console.error("[MCP] JSON parse error:", line.slice(0, 100));
        continue;
      }
      messages.push(msg);
      if (DEBUG) console.log("[MCP <=]", JSON.stringify(msg));
      listeners.forEach((l) => {
        try {
          l(msg);
        } catch {}
      });
    }
  });

  function send(msg) {
    const json = JSON.stringify(msg);
    if (DEBUG) console.log("[MCP =>]", json);
    // MCP uses simple newline-delimited JSON, not HTTP-style Content-Length
    proc.stdin.write(json + "\n");
  }

  let id = 0;
  const toolPref = (
    process.env.BRIGHTDATA_MCP_TOOL ||
    "search,unified_search,unified-search,config_check,diagnostics"
  ).split(",");
  let chosenTool = null;
  let finalResults = null;
  let completed = false;
  let configChecked = false;
  const timeoutMs = Number(process.env.BRIGHTDATA_MCP_TIMEOUT || 20000);

  const resultPromise = new Promise((resolve) => {
    const timeout = setTimeout(() => {
      if (!completed) {
        completed = true;
        resolve({
          error: `MCP timeout after ${timeoutMs}ms (stderr: ${stderrBuf.join("").slice(0, 200)})`,
        });
        try {
          proc.kill();
        } catch {}
      }
    }, timeoutMs);

    onMessage((msg) => {
      if (msg.method === "tools/list" && msg.params?.tools && !chosenTool) {
        // tools/list is a notification style from server; we just inspect.
        const names = msg.params.tools.map((t) => t.name);
        if (DEBUG) console.log(`[MCP] Available tools: ${names.join(", ")}`);

        // First try to run config diagnostics if available
        const configTool = names.find(
          (n) =>
            n.includes("config") ||
            n.includes("diagnostic") ||
            n.includes("status"),
        );
        if (configTool && !configChecked) {
          configChecked = true;
          if (DEBUG)
            console.log(
              `[MCP] Running configuration check with tool: ${configTool}`,
            );
          send({
            jsonrpc: "2.0",
            id: ++id,
            method: "tools/call",
            params: {
              name: configTool,
              arguments: {
                action: "evaluate_config",
                check_connection: true,
                workspace_path: process.cwd(),
                query: "configuration diagnostics for MCP connection issues",
              },
            },
          });
          return;
        }

        chosenTool = toolPref.find((p) => names.includes(p));
        if (!chosenTool) {
          completed = true;
          clearTimeout(timeout);
          resolve({
            error: `No preferred tool found (available: ${names.join(", ")})`,
          });
          try {
            proc.kill();
          } catch {}
          return;
        }
        send({
          jsonrpc: "2.0",
          id: ++id,
          method: "tools/call",
          params: { name: chosenTool, arguments: { query } },
        });
      } else if (msg.id && msg.result && msg.result.type === "tool_result") {
        // Standard tool result
        const data = msg.result.data;

        // Handle config check results
        if (configChecked && !chosenTool) {
          if (DEBUG)
            console.log(
              "[MCP] Configuration check result:",
              JSON.stringify(data, null, 2),
            );
          // Now proceed with actual search
          const names = msg.params?.tools?.map((t) => t.name) || toolPref;
          chosenTool = toolPref.find((p) => names.includes(p)) || names[0];
          if (chosenTool) {
            send({
              jsonrpc: "2.0",
              id: ++id,
              method: "tools/call",
              params: { name: chosenTool, arguments: { query } },
            });
            return;
          }
        }

        if (Array.isArray(data)) {
          finalResults = data.map((r) => ({
            title: r.title || r.name || "Untitled",
            url: r.url || r.link,
            snippet: r.snippet || r.summary,
            rank: r.rank,
          }));
        } else if (data && typeof data === "object") {
          // Single object
          finalResults = [
            {
              title: data.title || data.name || "Untitled",
              url: data.url || data.link,
              snippet: data.snippet || data.summary,
              rank: data.rank,
            },
          ];
        }
        completed = true;
        clearTimeout(timeout);
        resolve(
          finalResults && finalResults.length
            ? finalResults
            : { error: "Empty MCP tool result" },
        );
        try {
          proc.kill();
        } catch {}
      } else if (msg.error) {
        completed = true;
        clearTimeout(timeout);
        resolve({ error: `MCP error: ${msg.error.message || "unknown"}` });
        try {
          proc.kill();
        } catch {}
      }
    });
  });

  proc.on("exit", (code) => {
    if (!completed) {
      completed = true;
      const stderr = stderrBuf.join("").slice(0, 200) || "no stderr";
      finalResults = {
        error: `MCP process exited early (code ${code}). stderr: ${stderr}`,
      };
    }
  });

  // Initiate by asking for tools list (request form per JSON-RPC)
  send({ jsonrpc: "2.0", id: ++id, method: "tools/list" });
  return await resultPromise;
}

// --- Aggregation & Scoring ---
function aggregate(all) {
  const byUrl = new Map();
  for (const entry of all) {
    if (!entry || entry.error) continue;
    for (const r of entry.results || []) {
      const key = r.url;
      if (!key) continue;
      if (!byUrl.has(key)) {
        byUrl.set(key, { ...r, count: 1 });
      } else {
        const cur = byUrl.get(key);
        cur.count += 1;
        // Prefer shorter snippets if new one shorter but not empty (often cleaner)
        if (
          r.snippet &&
          (!cur.snippet || r.snippet.length < cur.snippet.length)
        )
          cur.snippet = r.snippet;
      }
    }
  }
  // Heuristic score: frequency weight + inverse rank weight
  const scored = [...byUrl.values()].map((v) => {
    const base = 1 / (v.rank || 1);
    const score = v.count * 1.25 + base;
    return { ...v, score: Number(score.toFixed(4)) };
  });
  scored.sort((a, b) => b.score - a.score);
  return scored;
}

function summarize(scored) {
  if (!scored.length) return "No aggregated results to summarize.";
  const top = scored
    .slice(0, 5)
    .map((r) => `- ${r.title} (${r.url}) [score=${r.score}]`)
    .join("\n");
  const themes = [];
  const text = scored.map((r) => `${r.title} ${r.snippet || ""}`.toLowerCase());
  if (text.some((t) => t.includes("license")))
    themes.push("Possible license initialization issue");
  if (text.some((t) => t.includes("xaml")))
    themes.push("Startup XAML resource / dictionary load issue");
  if (text.some((t) => t.includes("exit code")))
    themes.push("Uncaught early process termination");
  return `Top 5 Aggregated Results:\n${top}\n\nDetected Themes:\n${themes.map((t) => "• " + t).join("\n") || "• None"}\n`;
}

// --- Self-Test Harness (no external calls) ---
async function selfTest() {
  console.log("🧪 Running self-test (no external network)...");
  // Preserve originals
  const originalFetch = fetchFn;
  // Mock sequence: first call returns 401 (Bearer) to force fallback; second call returns success with results.
  let callCount = 0;
  fetchFn = async (url, opts) => {
    callCount++;
    const usingX = !!opts.headers["x-api-key"];
    if (callCount === 1 && !usingX) {
      return {
        ok: false,
        status: 401,
        text: async () => "unauthorized bearer",
      };
    }
    if (callCount === 2 && usingX) {
      return {
        ok: true,
        status: 200,
        json: async () => ({
          results: [
            {
              title: "Test Result",
              url: "https://example.com",
              snippet: "Snippet",
              rank: 1,
            },
          ],
        }),
      };
    }
    return { ok: false, status: 500, text: async () => "unexpected path" };
  };
  process.env.BRIGHTDATA_AUTO_HEADER = "1";
  const headerTest = await searchRest("header-fallback");
  const headerPass =
    Array.isArray(headerTest) &&
    headerTest[0]?.headerFallback === "used_x_api_key_after_bearer_failure";
  console.log(
    "Header fallback test:",
    headerPass ? "PASS" : "FAIL",
    `(calls=${callCount})`,
  );
  // Aggregation test
  const agg = aggregate([{ results: headerTest }]);
  console.log("Aggregation test:", agg.length === 1 ? "PASS" : "FAIL");
  // Summary test (should include top item title)
  const summary = summarize(agg);
  console.log(
    "Summary contains title:",
    summary.includes("Test Result") ? "PASS" : "FAIL",
  );
  // Restore fetch
  fetchFn = originalFetch;
  console.log("✅ Self-test complete");
}

async function run() {
  // Special mode for configuration checking
  if (process.env.BRIGHTDATA_CONFIG_CHECK === "1") {
    const configResult = await checkMcpConfig();
    console.log("\n📋 Configuration Check Results:");
    console.log(`Status: ${configResult.status}`);
    if (configResult.tools) {
      console.log(
        `Available tools: ${configResult.tools.map((t) => t.name).join(", ")}`,
      );
    }
    if (configResult.stderr) {
      console.log(`Stderr output: ${configResult.stderr.slice(0, 500)}...`);
    }
    console.log(`Recommendation: ${configResult.recommendation}`);
    return;
  }

  console.log(
    `🔍 BrightData Startup Diagnostics (Mode: ${MODE}, per-query limit: ${MAX_PER_QUERY})`,
  );
  if (MODE === "AUTO") {
    console.log(
      "ℹ AUTO mode: will attempt MCP tool first; if it errors or times out, will fallback to REST for that query.",
    );
  }
  if (MODE === "HOSTED") {
    console.log(
      "ℹ HOSTED mode: using Bright Data hosted MCP (adds --hosted automatically).",
    );
  }
  let effectiveEndpoint = API_ENDPOINT;
  if (AUTODETECT && MODE === "REST") {
    const candidates = [
      API_ENDPOINT,
      "https://api.brightdata.com/ai-tools/search",
      "https://api.brightdata.com/unified-search",
      "https://api.brightdata.com/v1/search",
      "https://api.brightdata.com/request",
    ].filter((v, i, a) => a.indexOf(v) === i); // unique
    console.log(
      `🧪 Autodetect enabled. Probing endpoints: \n  - ${candidates.join("\n  - ")}`,
    );
    for (const ep of candidates) {
      try {
        const probe = await fetchFn(ep, {
          method: "POST",
          headers: {
            ...(process.env.BRIGHTDATA_USE_X_HEADER === "1"
              ? { "x-api-key": API_KEY }
              : { Authorization: `Bearer ${API_KEY}` }),
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ query: "connectivity test", limit: 1 }),
        });
        if (probe.status === 401) {
          console.log(
            `  🔐 ${ep} -> 401 Unauthorized (this is GOOD: endpoint exists, key rejected)`,
          );
          effectiveEndpoint = ep;
          break;
        } else if (probe.ok) {
          console.log(`  ✅ ${ep} -> ${probe.status} OK`);
          effectiveEndpoint = ep;
          break;
        } else {
          const txt = await probe.text().catch(() => "");
          console.log(
            `  ❌ ${ep} -> ${probe.status} (${txt.slice(0, 80) || "no body"})`,
          );
        }
      } catch (e) {
        console.log(`  ⚠ ${ep} -> network error: ${e.message}`);
      }
    }
    if (effectiveEndpoint !== API_ENDPOINT) {
      console.log(`🔁 Using detected endpoint: ${effectiveEndpoint}`);
    } else {
      console.log(
        "ℹ No better endpoint detected; continuing with provided/default endpoint.",
      );
    }
  }
  // Override global for rest search runtime
  global.__BRIGHTDATA_EFFECTIVE_ENDPOINT = effectiveEndpoint;
  const collected = [];
  for (const q of QUERIES) {
    console.log(`\n➡ Query: ${q}`);
    let list;
    if (MODE === "REST") {
      // temporarily override constant via global var hack (avoid refactor)
      if (global.__BRIGHTDATA_EFFECTIVE_ENDPOINT) {
        // monkey patch local constant usage by shadowing function-scoped variable (simplest path)
        // eslint-disable-next-line no-unused-vars
        var API_ENDPOINT = global.__BRIGHTDATA_EFFECTIVE_ENDPOINT; // NOSONAR
      }
      list = await searchRest(q);
    } else if (MODE === "MCP" || MODE === "HOSTED") {
      list = await searchMcp(q);
    } else if (MODE === "AUTO") {
      // Try MCP first
      list = await searchMcp(q);
      if (list.error) {
        console.log(`  ⚠ MCP failed (${list.error}); falling back to REST`);
        if (global.__BRIGHTDATA_EFFECTIVE_ENDPOINT) {
          // eslint-disable-next-line no-unused-vars
          var API_ENDPOINT = global.__BRIGHTDATA_EFFECTIVE_ENDPOINT; // NOSONAR
        }
        const restList = await searchRest(q);
        // If REST also error, keep original MCP error but annotate
        if (!restList.error) list = restList;
        else list = { error: `MCP: ${list.error}; REST: ${restList.error}` };
      }
    } else {
      console.log(`  Unknown mode '${MODE}' – defaulting to REST`);
      if (global.__BRIGHTDATA_EFFECTIVE_ENDPOINT) {
        // eslint-disable-next-line no-unused-vars
        var API_ENDPOINT = global.__BRIGHTDATA_EFFECTIVE_ENDPOINT; // NOSONAR
      }
      list = await searchRest(q);
    }
    if (list.error) {
      console.log(`  ❌ ${list.error}`);
      collected.push({ query: q, error: list.error, results: [] });
      continue;
    }
    collected.push({ query: q, results: list });
    list.forEach((r, i) => {
      console.log(`  ${i + 1}. ${r.title}`);
      console.log(`     ${r.url}`);
      if (r.snippet) console.log(`     ${r.snippet.slice(0, 140)}...`);
    });
  }
  const aggregated = aggregate(collected);
  console.log("\n📊 Aggregated (unique URLs):", aggregated.length);
  aggregated.slice(0, 10).forEach((r, i) => {
    console.log(`  #${i + 1} [${r.score}] ${r.title} (${r.url})`);
  });
  if (SUMMARY) {
    console.log("\n🧠 Summary:\n" + summarize(aggregated));
  }
  if (OUTPUT_JSON) {
    try {
      const fs = require("fs");
      fs.writeFileSync(
        OUTPUT_JSON,
        JSON.stringify(
          {
            queries: QUERIES,
            collected,
            aggregated,
            generatedAt: new Date().toISOString(),
          },
          null,
          2,
        ),
      );
      console.log(`\n💾 Wrote JSON output -> ${OUTPUT_JSON}`);
    } catch (e) {
      console.log(`\n⚠ Failed to write ${OUTPUT_JSON}: ${e.message}`);
    }
  }
  console.log("\n✅ Diagnostics queries completed");
}
run().catch((e) => {
  console.error(e);
  process.exit(1);
});

// Execute self-test if requested (performed after main run to keep behavior predictable)
if (process.env.BRIGHTDATA_SELFTEST === "1") {
  selfTest().catch((e) => {
    console.error("Self-test error", e);
  });
}
