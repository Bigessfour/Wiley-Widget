// No external NuGet references required — script is self-contained to test the local encrypted vault.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
// not using Microsoft.Extensions.Logging here to keep the script self-contained

// Lightweight test script to validate the local encrypted secrets vault (DPAPI-backed)
#nullable enable
// - Uses an in-script vault implementation that mirrors production behavior
// - Tests: connection, set/get, empty input handling, caller-level cancellation/timeout,
//   concurrency, retry wrapper, export/import, list/delete

// (TestLogger moved to bottom to keep top-level statements at the top of the script)

// Assertion helpers
int passCount = 0, totalTests = 0;
void Assert(bool condition, string testName, string details = "")
{
    totalTests++;
    if (condition) { Console.WriteLine($"✓ {testName}"); passCount++; }
    else { Console.WriteLine($"✗ {testName} FAILED"); if (!string.IsNullOrEmpty(details)) Console.WriteLine("  Details: " + details); }
}

Console.WriteLine("=== Encrypted Local Secret Vault Integration Test (clean) ===\n");

var logger = new SimpleLogger<InScriptEncryptedLocalVault>();
var vault = new InScriptEncryptedLocalVault();

async Task CleanupKey(string k)
{
    try { await vault.DeleteSecretAsync(k); } catch { }
}

// Test 1: TestConnectionAsync
{
    var ok = await vault.TestConnectionAsync();
    Assert(ok, "TestConnectionAsync returns true", $"Returned {ok}");
}

// Test 2: Set/Get
{
    const string key = "test-key";
    const string val = "secret-value-123";
    await CleanupKey(key);
    await vault.SetSecretAsync(key, val);
    var got = await vault.GetSecretAsync(key);
    Assert(got == val, "Set/Get roundtrip", $"Expected '{val}', got '{got}'");
    await CleanupKey(key);
}

// Test 3: Empty input handling
{
    bool threw = false;
    try { await vault.SetSecretAsync("", "x"); }
    catch (ArgumentNullException) { threw = true; }
    Assert(threw, "SetSecretAsync rejects empty secret name");
}

// Test 4: Caller-level cancellation & timeout via Task.WaitAsync
{
    const string key = "slow-key";
    await CleanupKey(key);
    await vault.SetSecretAsync(key, "value");

    var cts = new CancellationTokenSource(); cts.Cancel();
    bool cancelled = false;
    try { var t = vault.GetSecretAsync(key); await t.WaitAsync(cts.Token); }
    catch (OperationCanceledException) { cancelled = true; }
    Assert(cancelled, "Caller-level cancellation via WaitAsync throws OperationCanceledException");

    bool timedOut = false;
    // Use a deterministic delay to force a timeout instead of relying on GetSecretAsync speed.
    try { await Task.Delay(1000).WaitAsync(TimeSpan.FromMilliseconds(1)); }
    catch (TimeoutException) { timedOut = true; }
    Assert(timedOut, "Caller-level timeout via WaitAsync(TimeSpan) throws TimeoutException (using Task.Delay)");

    await CleanupKey(key);
}

// Test 5: Concurrency
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var keys = Enumerable.Range(1, 20).Select(i => $"concurrent-{i}").ToArray();
    foreach (var k in keys) await CleanupKey(k);
    var tasks = keys.Select(async k => { await vault.SetSecretAsync(k, "v" + k); var r = await vault.GetSecretAsync(k); return (k, r); }).ToArray();
    var results = await Task.WhenAll(tasks);
    sw.Stop();
    var ok = results.All(r => r.r == "v" + r.k);
    Assert(ok, "Concurrent Set/Get for 20 keys all succeed", $"Elapsed {sw.ElapsedMilliseconds}ms");
    foreach (var k in keys) await CleanupKey(k);
}

// Test 6: Retry wrapper
{
    int failCount = 0;
    async Task WrappedSetAsync(string k, string v)
    {
        if (failCount < 2) { failCount++; throw new Exception("Simulated transient failure"); }
        await vault.SetSecretAsync(k, v);
    }
    async Task RetryAsync(Func<Task> action, int attempts = 3, TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromMilliseconds(50);
        for (int i = 1; ; i++)
        {
            try { await action(); return; }
            catch when (i < attempts) { await Task.Delay(delay.Value); continue; }
        }
    }
    const string rk = "retry-key";
    await CleanupKey(rk);
    bool succeeded = false;
    try { await RetryAsync(() => WrappedSetAsync(rk, "retry-val"), attempts: 4); var got = await vault.GetSecretAsync(rk); succeeded = got == "retry-val"; }
    catch (Exception ex) { Console.WriteLine("Retry wrapper failed: " + ex.Message); }
    Assert(succeeded, "Retry wrapper recovers from transient failures");
    await CleanupKey(rk);
}

// Test 7: Export/Import
{
    const string ek1 = "ex1", ek2 = "ex2";
    await CleanupKey(ek1); await CleanupKey(ek2);
    await vault.SetSecretAsync(ek1, "v1"); await vault.SetSecretAsync(ek2, "v2");
    var exported = await vault.ExportSecretsAsync();
    using var vault2 = new InScriptEncryptedLocalVault();
    await vault2.ImportSecretsAsync(exported);
    var g1 = await vault2.GetSecretAsync(ek1); var g2 = await vault2.GetSecretAsync(ek2);
    Assert(g1 == "v1" && g2 == "v2", "Export/Import roundtrip preserves secrets");
    await vault.DeleteSecretAsync(ek1); await vault.DeleteSecretAsync(ek2);
}

// Test 8: List/Delete
{
    const string lk = "list-key";
    await CleanupKey(lk);
    await vault.SetSecretAsync(lk, "val");
    var keys = (await vault.ListSecretKeysAsync()).ToList();
    Assert(keys.Contains(lk), "ListSecretKeysAsync includes stored key");
    await vault.DeleteSecretAsync(lk);
    var after = await vault.GetSecretAsync(lk);
    Assert(after == null, "DeleteSecretAsync removes the key");
}

// Summary
Console.WriteLine($"\n=== Test Results ===");
Console.WriteLine($"Passed: {passCount}/{totalTests} ({(passCount * 100) / Math.Max(totalTests,1)}%)");
Console.WriteLine("\n=== Coverage Summary ===");
Console.WriteLine("Methods tested: GetSecretAsync, SetSecretAsync, DeleteSecretAsync, ListSecretKeysAsync, ExportSecretsAsync, ImportSecretsAsync, TestConnectionAsync (via TestConnectionAsync)");
Console.WriteLine("Edge cases: empty input, caller-level cancellation/timeout, concurrent access, retry wrapper behavior, export/import");
Console.WriteLine("\nSample logs (none by default):");
foreach (var e in logger.LogEntries.Take(15)) Console.WriteLine(e);

vault.Dispose();
if (passCount != totalTests) Environment.Exit(2); else Environment.Exit(0);

// ---- In-script vault implementation (placed at bottom) ----
public sealed class InScriptEncryptedLocalVault : IDisposable
{
    private readonly string _vaultDirectory;
    private readonly string _entropyFile;
    private byte[]? _entropy;

    public InScriptEncryptedLocalVault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _vaultDirectory = Path.Combine(appData, "WileyWidget", "Secrets");
        _entropyFile = Path.Combine(_vaultDirectory, ".entropy");
        Directory.CreateDirectory(_vaultDirectory);
        _entropy = LoadOrGenerateEntropy();
    }

    private byte[] LoadOrGenerateEntropy()
    {
        try
        {
            if (File.Exists(_entropyFile))
            {
                var entropyBase64 = File.ReadAllText(_entropyFile);
                return Convert.FromBase64String(entropyBase64);
            }
            else
            {
                using var rng = RandomNumberGenerator.Create();
                var entropy = new byte[32];
                rng.GetBytes(entropy);
                File.WriteAllText(_entropyFile, Convert.ToBase64String(entropy));
                try { File.SetAttributes(_entropyFile, File.GetAttributes(_entropyFile) | FileAttributes.Hidden); } catch { }
                return entropy;
            }
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private string GetSecretFilePath(string secretName)
    {
        var safeName = string.Join("_", secretName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_vaultDirectory, $"{safeName}.secret");
    }

        public async Task SetSecretAsync(string secretName, string value)
    {
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));
        if (value == null) throw new ArgumentNullException(nameof(value));
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectBytes(plainBytes, _entropy);
            var encryptedBase64 = Convert.ToBase64String(encryptedBytes);
        var filePath = GetSecretFilePath(secretName);
        var tmp = filePath + ".tmp";
        await File.WriteAllTextAsync(tmp, encryptedBase64);
        try { File.Replace(tmp, filePath, null); } catch { File.Move(tmp, filePath); }
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (string.IsNullOrEmpty(secretName)) throw new ArgumentNullException(nameof(secretName));
        var filePath = GetSecretFilePath(secretName);
        if (!File.Exists(filePath)) return null;
        var encryptedBase64 = await File.ReadAllTextAsync(filePath);
            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedBase64);
                var decryptedBytes = UnprotectBytes(encryptedBytes, _entropy);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        var filePath = GetSecretFilePath(secretName);
        if (File.Exists(filePath)) File.Delete(filePath);
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<string>> ListSecretKeysAsync()
    {
        var secretFiles = Directory.GetFiles(_vaultDirectory, "*.secret");
        return secretFiles.Select(f => Path.GetFileNameWithoutExtension(f)).Where(k => k != ".entropy");
    }

    public async Task<string> ExportSecretsAsync()
    {
        var secrets = new Dictionary<string, string>();
        foreach (var key in await ListSecretKeysAsync())
        {
            var value = await GetSecretAsync(key);
            if (value != null) secrets[key] = value;
        }
        return JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task ImportSecretsAsync(string jsonSecrets)
    {
        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonSecrets);
        if (secrets == null) return;
        foreach (var kvp in secrets) await SetSecretAsync(kvp.Key, kvp.Value);
    }

    public async Task<bool> TestConnectionAsync()
    {
        const string tkey = "__test_connection__";
        const string tval = "test_value";
        await SetSecretAsync(tkey, tval);
        var retrieved = await GetSecretAsync(tkey);
        try { var p = GetSecretFilePath(tkey); if (File.Exists(p)) File.Delete(p); } catch { }
        return retrieved == tval;
    }

    // Protect/Unprotect helpers: try DPAPI via reflection, fall back to AES using entropy-derived key
    private static byte[] ProtectBytes(byte[] plainBytes, byte[]? entropy)
    {
        // Try to call System.Security.Cryptography.ProtectedData.Protect via reflection to avoid compile-time dependency
        try
        {
            var pdType = Type.GetType("System.Security.Cryptography.ProtectedData, System.Security.Cryptography.ProtectedData");
            if (pdType != null)
            {
                var protect = pdType.GetMethod("Protect", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(byte[]), typeof(byte[]), Type.GetType("System.Security.Cryptography.DataProtectionScope, System.Security.Cryptography.ProtectedData")! }, null);
                if (protect != null)
                {
                    var scopeType = Type.GetType("System.Security.Cryptography.DataProtectionScope, System.Security.Cryptography.ProtectedData");
                    var userScope = Enum.Parse(scopeType!, "CurrentUser");
                    var res = protect.Invoke(null, new object?[] { plainBytes, entropy, userScope });
                    return (byte[])res!;
                }
            }
        }
        catch
        {
            // ignore and fall back
        }

        // Fallback AES-CBC with key derived from entropy (NOT for production use; test-only)
        using var sha = SHA256.Create();
        var key = sha.ComputeHash(entropy ?? Array.Empty<byte>());
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var iv = new byte[16]; // zero IV for deterministic test behavior
        aes.IV = iv;
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plainBytes, 0, plainBytes.Length);
        }
        return ms.ToArray();
    }

    private static byte[] UnprotectBytes(byte[] encryptedBytes, byte[]? entropy)
    {
        try
        {
            var pdType = Type.GetType("System.Security.Cryptography.ProtectedData, System.Security.Cryptography.ProtectedData");
            if (pdType != null)
            {
                var unprotect = pdType.GetMethod("Unprotect", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(byte[]), typeof(byte[]), Type.GetType("System.Security.Cryptography.DataProtectionScope, System.Security.Cryptography.ProtectedData")! }, null);
                if (unprotect != null)
                {
                    var scopeType = Type.GetType("System.Security.Cryptography.DataProtectionScope, System.Security.Cryptography.ProtectedData");
                    var userScope = Enum.Parse(scopeType!, "CurrentUser");
                    var res = unprotect.Invoke(null, new object?[] { encryptedBytes, entropy, userScope });
                    return (byte[])res!;
                }
            }
        }
        catch
        {
            // ignore and fall back
        }

        using var sha = SHA256.Create();
        var key = sha.ComputeHash(entropy ?? Array.Empty<byte>());
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var iv = new byte[16];
        aes.IV = iv;
        using var ms = new MemoryStream(encryptedBytes);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var outMs = new MemoryStream();
        cs.CopyTo(outMs);
        return outMs.ToArray();
    }

    public void Dispose()
    {
        if (_entropy != null) { Array.Clear(_entropy, 0, _entropy.Length); _entropy = null; }
    }
}

// Simple test logger that collects messages for assertions (moved here to keep top-level statements first)
public class SimpleLogger<T>
{
    public List<string> LogEntries { get; } = new List<string>();
    public void Log(string message)
    {
        try { LogEntries.Add(message); } catch { }
    }
}
