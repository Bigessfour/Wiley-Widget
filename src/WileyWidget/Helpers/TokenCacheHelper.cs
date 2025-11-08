using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Identity.Client;

namespace WileyWidget;

/// <summary>
/// Helper class for token cache serialization and persistence
/// </summary>
public static class TokenCacheHelper
{
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WileyWidget",
        "msalcache.bin3");

    /// <summary>
    /// Enables token cache serialization for the specified public client application
    /// </summary>
    public static void EnableSerialization(IPublicClientApplication app)
    {
        // Create directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);

        app.UserTokenCache.SetBeforeAccess(BeforeAccessNotification);
        app.UserTokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
#pragma warning disable CA1416 // ProtectedData is only supported on Windows
        try
        {
            if (File.Exists(CacheFilePath))
            {
                byte[] encryptedData = File.ReadAllBytes(CacheFilePath);
                byte[] data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                args.TokenCache.DeserializeMsalV3(data);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - cache corruption shouldn't break authentication
            System.Diagnostics.Debug.WriteLine($"Error reading token cache: {ex.Message}");
        }
#pragma warning restore CA1416
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
#pragma warning disable CA1416 // ProtectedData is only supported on Windows
        try
        {
            // Always serialize the cache after access to ensure persistence
            byte[] data = args.TokenCache.SerializeMsalV3();
            byte[] encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CacheFilePath, encryptedData);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - cache write failure shouldn't break authentication
            System.Diagnostics.Debug.WriteLine($"Error writing token cache: {ex.Message}");
        }
#pragma warning restore CA1416
    }
}
