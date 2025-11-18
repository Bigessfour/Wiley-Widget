using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Landing page (useful for Host Domain verification during development)
app.MapGet("/", () => Results.Content("<html><body><h3>Wiley Widget (Dev)</h3><p>Webhooks receiver is running.</p></body></html>", "text/html"));

// App launch URL (Intuit settings → Launch URL)
app.MapGet("/app/launch", () => Results.Content("<html><body><h3>Wiley Widget</h3><p>Launch acknowledged.</p></body></html>", "text/html"));

// App disconnect URL (Intuit settings → Disconnect URL)
app.MapGet("/app/disconnect", () => Results.Content("<html><body><h3>Wiley Widget</h3><p>Disconnect acknowledged.</p></body></html>", "text/html"));

// Privacy Policy (for Intuit settings) - simple placeholder for sandbox/dev
app.MapGet("/privacy", () => Results.Content(
    "<html><body><h3>Wiley Widget - Privacy Policy (Sandbox)</h3><p>This is a development placeholder. No personal data is collected by this dev endpoint.</p></body></html>",
    "text/html"));

// End-User License Agreement (for Intuit settings) - simple placeholder for sandbox/dev
app.MapGet("/eula", () => Results.Content(
    "<html><body><h3>Wiley Widget - EULA (Sandbox)</h3><p>Development placeholder EULA. By using this sandbox, you agree this is for testing only.</p></body></html>",
    "text/html"));

// Intuit Webhooks endpoint
// Official docs: X-Intuit-Signature header: base64(HMACSHA256(body, webhooks-verifier-token))
app.MapPost("/qbo/webhooks", async (HttpRequest req) =>
{
    // Read raw body
    using var reader = new StreamReader(req.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();

    // Get signature header
    var signatureHeader = req.Headers["intuit-signature"].FirstOrDefault()
                         ?? req.Headers["X-Intuit-Signature"].FirstOrDefault();

    var secret = Environment.GetEnvironmentVariable("QBO_WEBHOOKS_VERIFIER");
    if (!string.IsNullOrWhiteSpace(secret))
    {
        try
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = h.ComputeHash(Encoding.UTF8.GetBytes(body ?? string.Empty));
            var expected = Convert.ToBase64String(hash);
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                Console.WriteLine("[QBO Webhook] Missing signature header; rejecting.");
                return Results.Unauthorized();
            }
            // Compare base64 values in constant time
            var providedBytes = Convert.FromBase64String(signatureHeader);
            if (!CryptographicOperations.FixedTimeEquals(providedBytes, hash))
            {
                Console.WriteLine("[QBO Webhook] Signature mismatch; rejecting.");
                return Results.Unauthorized();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QBO Webhook] Signature validation error: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
    else
    {
        Console.WriteLine("[QBO Webhook] QBO_WEBHOOKS_VERIFIER not set; accepting without signature validation (dev only).");
    }

    Console.WriteLine($"[QBO Webhook] OK len={body?.Length ?? 0}");
    return Results.Ok();
});

app.Run();
