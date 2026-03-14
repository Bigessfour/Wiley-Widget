# QuickBooks Production Cutover Guide

This guide is for the moment Wiley Widget is ready to stop using QuickBooks sandbox data and connect to the real QuickBooks Online company that holds production Wiley Widget data.

It is based on Intuit's official guidance for:

- separate Development and Production credentials
- Production redirect URI requirements
- Production app settings and accepted connections
- production key approval before real credentials are issued

## What Changes In Production

Sandbox and production are not the same switch with the same credentials.

- Development credentials only work with sandbox companies.
- Production credentials only work after Intuit enables them for the app.
- Production redirect URIs must be configured separately from sandbox redirect URIs.
- Production redirect URIs must use HTTPS.
- Production redirect URIs cannot use IP addresses.
- Users must authorize the production app again against the real QuickBooks company.

## Production Readiness Gate

Do not start cutover until all of these are true:

- Sandbox connect and disconnect work end to end in Wiley Widget.
- QuickBooks diagnostics report valid sandbox configuration.
- The app handles QuickBooks outages and auth failures without hanging.
- The exact production callback strategy is decided.

For Wiley Widget, the callback strategy must be one of these:

- A production HTTPS callback endpoint controlled by Wiley Widget.
- A loopback HTTPS callback with a locally trusted TLS certificate and HTTP.SYS SSL binding.

Do not attempt production cutover with `http://localhost:5000/callback/`. That is a sandbox setup.

## Step 1. Finish Intuit Production Enablement

In the Intuit Developer Portal:

1. Open the QuickBooks app.
2. Go to Keys and credentials.
3. Switch from Development to Production.
4. Complete the Production Key questionnaire if Intuit shows it.
5. Wait for approval.
6. Copy the Production Client ID and Production Client Secret after approval.

Important:

- Production keys are separate from sandbox keys.
- Do not mix sandbox and production credentials.

## Step 2. Configure Production App Settings In Intuit

Open Settings for the same app and set the Production values, not only Development values.

Verify these sections:

- App name and logo
- Host domain
- Launch URL
- Disconnect URL
- Privacy policy URL
- Terms of service URL
- Accepted connections / supported countries
- Production redirect URI
- Scopes

Notes:

- The Production redirect URI must exactly match what Wiley Widget will use at runtime.
- The match includes scheme, host, port, path, casing, and trailing slash.
- If scopes change, users must reauthorize.

## Step 3. Decide The Production Callback URI

You need a production-safe callback before real-company auth will work.

### Option A. Hosted Wiley Widget HTTPS callback

Use a Wiley-controlled HTTPS endpoint such as a production app domain callback route.

Use this when:

- you already have server infrastructure
- you want a centrally managed callback flow
- you do not want to manage local desktop TLS bindings

### Option B. HTTPS loopback callback

Use an HTTPS loopback endpoint such as `https://localhost:5001/callback/`.

Use this only if:

- the URI is registered in Intuit Production settings
- the machine trusts the TLS certificate
- HTTP.SYS has the required SSL certificate binding on that port
- the desktop app can successfully host that listener

If you choose HTTPS loopback, the sandbox URL ACL step is not enough by itself. You also need SSL certificate binding.

## Step 4. Update Wiley Widget Configuration

Switch Wiley Widget from sandbox values to production values.

At minimum, update:

- `Services:QuickBooks:OAuth:ClientId`
- `Services:QuickBooks:OAuth:ClientSecret`
- `Services:QuickBooks:OAuth:RedirectUri`
- `Services:QuickBooks:OAuth:Environment`

Production values should become:

- `ClientId`: Production Client ID
- `ClientSecret`: Production Client Secret
- `RedirectUri`: exact Production redirect URI
- `Environment`: `production`

Do not reuse:

- sandbox access tokens
- sandbox refresh tokens
- sandbox realm IDs

## Step 5. Prepare The Local Machine If Using Loopback HTTPS

If production uses a loopback HTTPS callback, prepare the machine before first sign-in:

1. Create or install a certificate trusted by the local machine.
2. Bind the certificate to the chosen port in HTTP.SYS.
3. Ensure the listener URL ACL exists for the callback URI.
4. Confirm Wiley Widget can bind the HTTPS listener before you attempt Connect.

If this setup is missing, production auth will fail before the callback completes.

## Step 6. Run Pre-Cutover Diagnostics

In Wiley Widget:

1. Open the QuickBooks panel.
2. Run Diagnostics.
3. Confirm the environment reads `production`.
4. Confirm the redirect URI shown by the app is the exact production URI.
5. Confirm credentials are present.
6. Confirm the redirect guidance shows no production mismatch.
7. If using loopback, confirm the local listener prerequisites are satisfied.

Do not proceed if diagnostics still show a sandbox redirect URI, HTTP redirect URI, missing credentials, or missing local callback prerequisites.

## Step 7. Connect To The Real QuickBooks Company

1. Click Connect in Wiley Widget.
2. Sign in with an administrator account for the real QuickBooks Online company.
3. Complete consent for the production app.
4. Wait for the callback to return control.
5. Confirm Wiley Widget reports a connected state.
6. Confirm the real company realm ID is captured.

The first production connect is the point where Wiley Widget moves from sandbox data to real company data.

## Step 8. Validate Real-Data Proof

After production auth succeeds, validate with real data immediately:

1. Run a lightweight company info or connection test.
2. Open the QuickBooks panel and confirm connected status messaging is clear.
3. Pull a minimal real-data query.
4. Confirm the returned company is the Wiley production company, not the sandbox company.
5. Confirm disconnect also works cleanly.
6. Reconnect once to prove the flow is repeatable.

## Step 9. Record The Cutover

For the release record, capture:

- date of production key approval
- production redirect URI used
- whether callback is hosted HTTPS or HTTPS loopback
- evidence that a real-company connect succeeded
- evidence that disconnect and reconnect succeeded
- the realm ID of the real company, stored securely and not committed

## Stop Conditions

Stop the cutover and treat it as blocked if any of these occur:

- Production Client ID and Client Secret are not available yet.
- The Intuit Production redirect URI does not exactly match Wiley Widget runtime config.
- The redirect URI is still HTTP.
- The redirect URI is an IP address.
- The production callback cannot return control to Wiley Widget.
- The app still shows sandbox realm ID or sandbox company data.
- Real-data connection succeeds once but cannot reconnect reliably.

## Summary

The production move is not just changing `sandbox` to `production`.

The required order is:

1. complete sandbox proof
2. obtain Intuit production approval and production keys
3. configure Production settings and redirect URI in Intuit
4. deploy or prepare a production-safe HTTPS callback
5. switch Wiley Widget config to production values
6. run diagnostics
7. authorize against the real company
8. prove real-data connect, disconnect, and reconnect
