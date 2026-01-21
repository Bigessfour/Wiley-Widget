# WileyWidget.Webhooks (QuickBooks Online)

Minimal receiver for Intuit QBO Webhooks in development.

## Run locally

- Start the project (Debug or `dotnet run`), it will listen on:
  - HTTPS: <https://localhost:7207>
  - HTTP: <http://localhost:5207>
- Health check: <https://localhost:7207/health>

## Webhooks endpoint to use in Intuit portal

- Preferred (with Cloudflare Tunnel):
  - <https://app.townofwiley.gov/qbo/webhooks>
- Local-only testing (not reachable by Intuit):
  - <https://localhost:7207/qbo/webhooks>

Note: Intuit requires a public HTTPS endpoint. Use Cloudflare Tunnel (recommended) to map your domain to the local port 7207. Health check should be available at:

- <https://app.townofwiley.gov/health>

## App settings (Intuit portal) quick reference

- Host domain (no protocol): `app.townofwiley.gov`
- Launch URL: `https://app.townofwiley.gov/app/launch`
- Disconnect URL: `https://app.townofwiley.gov/app/disconnect`
- Privacy Policy URL (sandbox placeholder): `https://app.townofwiley.gov/privacy`
- EULA URL (sandbox placeholder): `https://app.townofwiley.gov/eula`
- Redirect URIs (OAuth): `http://localhost:8080/callback`

## Signature verification

- Set environment variable `QBO_WEBHOOKS_VERIFIER` to your Webhooks key from the Intuit portal
- Signature validation is implemented in Program.cs: validates `X-Intuit-Signature` header as HMACSHA256(body, verifier) using constant-time comparison
- Requests without a valid signature are rejected with 401 Unauthorized

## Production

- Host this endpoint on a public HTTPS domain you control (e.g., app.townofwiley.gov via Cloudflare).
- Enforce signature validation (X-Intuit-Signature) and add structured logging.
