# Model Context Protocol (MCP) — Proper Application in Wiley Widget

This guide summarizes the official MCP best practices and maps them to our current “GitHub MCP + Trunk + CI” workflow so we apply MCP correctly and predictably.

References (authoritative):
- MCP Documentation site: https://modelcontextprotocol.io/
- MCP Specification index: https://spec.modelcontextprotocol.io/
  - Current protocol version: 2025-06-18
- MCP Specification repository: https://github.com/modelcontextprotocol/modelcontextprotocol

## What matters most for this repo

- Align on the current MCP spec version (2025-06-18) and concepts: Clients, Servers, Initialization, Version Negotiation, Tools, Resources, and Prompts.
- Treat “tools” as typed, schema-driven operations with explicit inputs/outputs and clear side-effect boundaries.
- Separate read-only tools (safe to auto-run) from mutating tools (require explicit confirmation or extra safeguards).
- Build resilient loops with rate limiting, retries, pagination, and failure classification.
- Log tool calls minimally (no secrets) with correlation IDs for traceability.

## Quick mapping to our current workflow

Our approved loop (see docs and copilot instructions) uses “GitHub MCP” tools to:
1) List recent CI runs and job statuses
2) Pull failed job logs
3) Run Trunk fixes locally
4) Re-run CI and monitor until green

This approach is MCP-aligned if we enforce the following:

- Version negotiation: Prefer/assume MCP 2025-06-18. Clients/servers should negotiate and abort cleanly on mismatch.
- Tool contracts: Each MCP tool (e.g., list_workflow_runs, get_job_logs) should have a stable JSON schema for inputs and outputs.
- Idempotency: Read-only introspection tools are safe; mutating actions (e.g., re-run workflows) should be gated with a confirm step or policy.
- Pagination & backoff: CI logs, runs, and artifacts can be large—always request with page/limit and exponential backoff on 429/5xx.
- Time-bounded polling: For watch flows, use bounded retry windows and report status clearly.
- Least-privilege auth: Use tokens scoped only to read workflows and logs unless explicitly required for reruns or dispatch.
- Secret handling: Never echo tokens, secrets, or sensitive log lines in MCP messages or persisted logs.

## Minimal “tool contract” checklist

For each MCP tool your client uses, ensure:
- Inputs documented with JSON schema (types, enums, optional/required, defaults)
- Outputs documented with JSON schema (including possible empty/partial results)
- Error model: distinguish retriable (rate limit, transient network) vs. terminal (auth, bad request)
- Pagination parameters: page, per_page/limit, next token
- Timeout behavior: server-side timeouts and client-side cancellation
- Idempotency note and side-effects (if any)

## Observability & auditability

- Attach a correlationId to each MCP call and include in local logs
- Record: tool name, inputs hash (redacted), duration, status, error class (if any)
- Redact secrets rigorously; avoid writing raw CI logs if they may contain credentials

## Resiliency patterns for CI monitoring via MCP

- Rate limits: Use exponential backoff with jitter on 429s; surface wait times in UI/logs
- Partial failures: If fetching logs for one failed job stalls, continue with others and report partial results
- Timeouts: Use smaller timeouts for list calls; larger for log download; always support cancellation
- Retries: Cap retries to a small number (e.g., 3) and surface the final error with actionable hints

## Security guidelines

- Token scopes: Prefer read-only workflow scopes. Only enable re-run/dispatch when necessary
- Rotations: Rotate tokens regularly; store outside code (GitHub secrets, local keychain)
- Data minimization: Fetch only what’s needed (e.g., latest N runs; only failed job logs)

## Concrete recommendations for this repo

1) Add a tiny MCP config descriptor for documentation
   - docs-only metadata that states: preferred MCP version (2025-06-18), known servers (GitHub), and the tool surface used (list runs, get job logs). This is not required by the spec but improves clarity for contributors.

2) Tighten tool usage in automation scripts
   - Where we rely on “GitHub MCP” tools, ensure our wrapper functions pass: pagination params; handle 429/5xx with backoff; and bound overall wait times when watching CI.

3) Update the CI docs to call out versioning
   - Note the current MCP version and link to the spec’s versioning page. Clarify that clients/servers should negotiate and fall back or abort.

4) Secret hygiene in logs
   - Reiterate that MCP call logs must redact arguments and outputs which may contain secrets; avoid storing raw CI log lines at rest unless scrubbed.

5) Optional: Add a thin “contract” doc for the MCP GitHub tools
   - Define the expected inputs/outputs for list_workflow_runs and get_job_logs as we use them (types, defaults, constraints). This can live under docs/mcp/contracts/.

## Pointers to the spec sections you’ll use most

- Versioning: https://modelcontextprotocol.io/specification/versioning
- Lifecycle (initialization & negotiation): https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle
- Tools (schema-driven actions): https://modelcontextprotocol.io/specification/2025-06-18/tools
- Resources & Prompts (if adopted later): https://modelcontextprotocol.io/docs/learn/architecture

## Lightweight example: CI watch loop with MCP

- Read-only tools: list_workflow_runs (latest 1-5), get_job_logs (failed-only)
- Behavior:
  - Poll every 30–60s up to 15 minutes, then stop
  - Back off on 429/5xx (1s, 2s, 4s, 8s…) with jitter
  - Summarize failures and only then trigger local trunk fixes

This keeps our “self-healing” loop aligned with MCP conventions while remaining simple and robust.

---
If we adopt additional MCP servers (e.g., issue triage, artifact indexing), extend the same contracts, security, and observability principles.
