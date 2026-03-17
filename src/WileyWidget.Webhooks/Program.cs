using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

// Load configuration from appsettings.json and user secrets
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();
app.UseStaticFiles();

var webPagesRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

static IResult HtmlPage(string webRootPath, string fileName)
{
    var path = Path.Combine(webRootPath, "pages", fileName);
    return Results.File(path, "text/html; charset=utf-8");
}

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Landing page
app.MapGet("/", () => HtmlPage(webPagesRoot, "index.html"));

// App launch URL (Intuit settings → Launch URL)
app.MapGet("/app/launch", () => HtmlPage(webPagesRoot, "launch.html"));

// App disconnect URL (Intuit settings → Disconnect URL)
app.MapGet("/app/disconnect", () => HtmlPage(webPagesRoot, "disconnect.html"));

// Privacy Policy
app.MapGet("/privacy", () => HtmlPage(webPagesRoot, "privacy.html"));

// End-User License Agreement
app.MapGet("/eula", () => HtmlPage(webPagesRoot, "eula.html"));

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

    // Priority: User Secrets > Environment > appsettings.json
    var secret = app.Configuration["Services:QuickBooks:Webhooks:VerifierToken"]
                ?? Environment.GetEnvironmentVariable("QBO_WEBHOOKS_VERIFIER")
                ?? app.Configuration["Services:QuickBooks:Webhooks:VerifierToken"];

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
        var environment = app.Configuration["Services:QuickBooks:OAuth:Environment"] ?? "sandbox";
        if (environment.Equals("production", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[QBO Webhook] ERROR: Production mode requires VerifierToken in user secrets!");
            return Results.StatusCode(503); // Service Unavailable - misconfigured
        }
        Console.WriteLine("[QBO Webhook] Sandbox mode: accepting without signature validation (dev only).");
    }

    Console.WriteLine($"[QBO Webhook] OK len={body?.Length ?? 0}");
    return Results.Ok();
});

app.Run();
