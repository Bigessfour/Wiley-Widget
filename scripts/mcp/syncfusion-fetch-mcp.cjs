#!/usr/bin/env node
'use strict';

// Lightweight Syncfusion docs HTTP proxy for MCP integration
// - Exposes a health endpoint at /health
// - Accepts requests like GET /datagrid or GET /fetch?path=datagrid and fetches
//   content from SYNCFUSION_BASE_URL + path or path/overview etc.
// - Returns a tiny markdown summary (title + first paragraph) for quick validation

const http = require('http');
const { URL } = require('url');

const BASE = process.env.SYNCFUSION_BASE_URL || 'https://help.syncfusion.com/windowsforms/';
const PORT = parseInt(process.env.PORT || '61032', 10); // default fixed port for MCP http health checks

async function tryFetchPaths(base, controlPath) {
  const candidates = [
    `${controlPath}`,
    `${controlPath}/overview`,
    `${controlPath}/getting-started`,
    `${controlPath}/concepts`,
    `${controlPath}/examples`,
  ];

  for (const p of candidates) {
    let url;
    try {
      url = new URL(p, base).toString();
    } catch (err) {
      continue;
    }

    try {
      const resp = await fetch(url, { method: 'GET' });
      if (!resp.ok) continue;
      const body = await resp.text();
      // treat long-enough content as success
      if (body && body.length > 50) return { url, body };
    } catch (err) {
      // ignore and try next
      continue;
    }
  }

  return null;
}

function stripHtmlAndCollapse(s) {
  if (!s) return '';
  // remove scripts/styles
  s = s.replace(/<script[\s\S]*?<\/script>/gi, '');
  s = s.replace(/<style[\s\S]*?<\/style>/gi, '');
  // remove tags
  s = s.replace(/<[^>]+>/g, '');
  // collapse whitespace
  return s.replace(/\s+/g, ' ').trim();
}

function extractMarkdown(html, sourceUrl) {
  const titleMatch = html.match(/<title>([\s\S]*?)<\/title>/i);
  const h1Match = html.match(/<h1[^>]*>([\s\S]*?)<\/h1>/i);
  const h2Match = html.match(/<h2[^>]*>([\s\S]*?)<\/h2>/i);
  const pMatch = html.match(/<p[^>]*>([\s\S]*?)<\/p>/i);

  const title = stripHtmlAndCollapse(titleMatch ? titleMatch[1] : h1Match ? h1Match[1] : h2Match ? h2Match[1] : '');
  const firstParagraph = stripHtmlAndCollapse(pMatch ? pMatch[1] : '');

  let md = '';
  if (title) md += `# ${title}\n\n`;
  if (firstParagraph) md += `${firstParagraph}\n\n`;
  md += `Source: ${sourceUrl}\n`;
  md += `FetchedFrom: ${BASE}\n`;

  return md;
}

const server = http.createServer(async (req, res) => {
  try {
    const urlObj = new URL(req.url, `http://${req.headers.host}`);
    const path = urlObj.pathname.replace(/^\/+/, '');

    // health
    if (path === 'health' || path === '') {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ status: 'ok', service: 'syncfusion-docs-proxy', base: BASE }));
      return;
    }

    // support /fetch?path=... or direct /datagrid
    let q = urlObj.searchParams.get('path') || urlObj.searchParams.get('q') || path;
    if (!q) {
      res.writeHead(400, { 'Content-Type': 'text/plain' });
      res.end('Missing query path');
      return;
    }

    q = q.replace(/^\/+|\/+$/g, '');

    // attempt to fetch several candidate URLs
    const fetched = await tryFetchPaths(BASE, q);
    if (!fetched) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end(`No documentation found for ${q}`);
      return;
    }

    const md = extractMarkdown(fetched.body, fetched.url);
    res.writeHead(200, { 'Content-Type': 'text/markdown; charset=utf-8' });
    res.end(md);
  } catch (err) {
    res.writeHead(500, { 'Content-Type': 'text/plain' });
    res.end('Internal error: ' + String(err));
  }
});

server.listen(PORT, () => {
  const addr = server.address();
  const port = typeof addr === 'object' ? addr.port : addr;
  console.log(`syncfusion-fetch-mcp.cjs listening on port ${port} (base=${BASE})`);
});

// keep node process alive
setInterval(() => {}, 60_000);
