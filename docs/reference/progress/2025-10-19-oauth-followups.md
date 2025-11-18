# OAuth follow-ups (QuickBooks integration)

Status: In progress
Owner: Settings/OAuth
Date: 2025-10-19

Tasks

- [ ] Auto urlacl binding fix
  - Detect binding failures for the local redirect URI.
  - Offer/execute `netsh http add urlacl` with the exact URL and account.
  - Log command, output, and result; re-verify on completion.
- [ ] Post-token verification and persistence
  - Validate token receipt and scopes.
  - Persist securely; surface status in Settings > QuickBooks Integration.
  - Add retry/backoff for service calls with clear UX messages.

Notes

- Threading cleanup is complete across core ViewModels; this file owns the remaining OAuth work separately from threading.
