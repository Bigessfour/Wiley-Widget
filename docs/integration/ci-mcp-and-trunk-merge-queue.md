## CI, GitHub MCP, and Trunk Merge Queue — quick guide

This repository includes helpful tooling to integrate CI, a local/remote GitHub MCP server and an opinionated Trunk merge-queue workflow.

What we added

- A small helper script: `scripts/ci/report-to-mcp.ps1` — safely posts a compact JSON summary for workflow runs to an MCP endpoint (non-fatal if not configured).
- A consolidated CI workflow: `.github/workflows/ci-consolidated.yml` — runs Trunk checks and a Windows build/test job, uploads artifacts, and (optionally) reports to MCP when `MCP_ENDPOINT`/`MCP_TOKEN` secrets are configured.
- A Trunk Merge Queue helper workflow: `.github/workflows/trunk-merge-queue.yml` — listens to PR comments or label events and will add/remove `trunk-merge-queued` labels; if `TRUNK_API_URL`/`TRUNK_API_TOKEN` are configured, the workflow will POST add/remove requests to the configured Trunk API.

How to exercise the GitHub MCP for actions reporting (local testing)

1. Start the local Wiley GH MCP server for development (from the repo root):

   pwsh scripts/tools/start-wiley-gh-mcp.ps1 -Background

2. Ensure the MCP server is reachable and note the endpoint (default: <http://127.0.0.1:6723/actions/report> or similar — check the server manifest in `.continue/mcpServers/*`).

3. In your GitHub repository's Secrets (on GitHub) add:
   - `MCP_ENDPOINT` — the full MCP endpoint URL to receive CI summaries.
   - `MCP_TOKEN` — optional bearer token if your MCP is protected.

4. The consolidated CI workflow (`ci-consolidated.yml`) will POST metadata to the MCP endpoint when the repository secret `MCP_ENDPOINT` is set — no change required in the workflow file.

How to enable Trunk Merge Queue integration

1. The helper workflow `trunk-merge-queue.yml` will: add/remove the `trunk-merge-queued` label when a PR comment contains `/enter-merge-queue` or `/leave-merge-queue`.
2. To wire a real Trunk Merge Queue API, set the repository secrets:
   - `TRUNK_API_URL` — base URL for your Trunk Merge Queue HTTP API.
   - `TRUNK_API_TOKEN` — token to authenticate to Trunk API.
3. Once configured, comments like `/enter-merge-queue` will cause the workflow to forward add/remove requests to your Trunk API endpoint.

Notes & next steps

- The provided scripts and workflows are intentionally opt-in. Without the secrets above, the features are safe and non-fatal so they won't break CI runs.
- For full enforcement, protect the `main` branch and use GitHub branch protection to require a `trunk-merge-queued` label or the Trunk Merge Queue GitHub App before merging.
- If you'd like, I can (a) wire the trunk-action/queue integration directly (if you use Trunk's hosted merge queue), or (b) make the workflow call your specific Trunk API endpoint or CLI based on your Trunk setup.
