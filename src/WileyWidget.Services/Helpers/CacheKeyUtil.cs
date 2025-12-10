using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WileyWidget.Services.Helpers
{
    /// <summary>
    /// Utility helpers for building stable, deterministic cache keys.
    /// Avoids the use of string.GetHashCode (which is runtime dependent) by
    /// using a canonical normalization + SHA-256 hashing. Keys are versioned
    /// so you can invalidate caches after prompt/template changes.
    /// </summary>
    public static class CacheKeyUtil
    {
        /// <summary>
        /// Generate a stable cache key using a prefix and ordered inputs.
        /// Example result: "XAI:v1:9f86d081884c7..."
        /// </summary>
        /// <param name="prefix">Prefix such as "XAI" or "Grok.FetchEnterpriseData".</param>
        /// <param name="version">Cache key version string (defaults to "v1").</param>
        /// <param name="inputs">Ordered inputs that should be part of the key. Nulls are treated as empty strings.</param>
        /// <returns>Deterministic cache key string.</returns>
        public static string Generate(string prefix, string version = "v1", params string?[] inputs)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentNullException(nameof(prefix));
            if (version == null) version = "v1";

            // Canonicalize and normalize inputs
            var normalized = inputs?.Select(i => Canonicalize(i ?? string.Empty)) ?? Enumerable.Empty<string>();

            var combined = string.Join("|", normalized);

            // Compute SHA-256 hash and return hex representation
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hash = sha.ComputeHash(bytes);
            var hex = ToHex(hash);

            return $"{prefix}:{version}:{hex}";
        }

        private static string Canonicalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Trim and normalize whitespace then lower-case for canonicalization
            var collapsed = System.Text.RegularExpressions.Regex.Replace(input.Trim(), "\\s+", " ");
            return collapsed.ToLowerInvariant();
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
