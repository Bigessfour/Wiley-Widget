# Environment Scope Policy

## Purpose

This policy defines environment-variable governance for Wiley Widget runtime and tooling.

## Scope Model

- Machine scope is the canonical source of truth.
- User and Process scopes are compatibility-only during migration.
- Runtime and MCP configuration should resolve canonical machine names first.

## Canonical Variables

- `GITHUB_PERSONAL_ACCESS_TOKEN`
- `MSSQL_CONNECTION_STRING`
- `DATABASE_CONNECTION_STRING`
- `SYNCFUSION_MCP_API_KEY`
- `SYNCFUSION_LICENSE_KEY`
- `XAI__ApiKey`
- `XAI_BASE_URL`
- `QBO_CLIENT_ID`
- `QBO_CLIENT_SECRET`
- `QBO_REDIRECT_URI`
- `QBO_ENVIRONMENT`
- `QBO_WEBHOOKS_VERIFIER`
- `WW_REPO_ROOT`
- `ASPNETCORE_ENVIRONMENT`
- `WILEYWIDGET_DEFAULT_FISCAL_YEAR`
- `WILEYWIDGET_SETTINGS_DIR`
- `SYNCFUSION_SILENT_LICENSE_VALIDATION`

## Compatibility Aliases (Temporary)

- `GITHUB_TOKEN`, `GITHUB_PAT` -> `GITHUB_PERSONAL_ACCESS_TOKEN`
- `SYNCFUSION_API_KEY` -> `SYNCFUSION_MCP_API_KEY`
- `XAI_API_KEY`, `WILEYWIDGET_XAI_API_KEY` -> `XAI__ApiKey`
- `QUICKBOOKS_CLIENT_ID` -> `QBO_CLIENT_ID`
- `QUICKBOOKS_CLIENT_SECRET` -> `QBO_CLIENT_SECRET`
- `QUICKBOOKS_REDIRECT_URI` -> `QBO_REDIRECT_URI`
- `QUICKBOOKS_ENVIRONMENT` -> `QBO_ENVIRONMENT`

## Operational Commands

- Align machine variables: `./scripts/setup-env.ps1 -EnvFilePath .env.machine -PromoteFromUser -IncludeCompatibilityAliases`
- Audit alignment: `./scripts/tools/audit-runtime-env.ps1 -OutputCsv tmp/runtime-env-audit-canonical.csv`

## Migration Guidance

- New scripts/config must write canonical names to Machine scope.
- Existing aliases remain readable during compatibility window.
- Remove aliases after two stable releases with no drift warnings from audit script.
