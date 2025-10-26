// Archived original: moved to _archived/azure/WileyWidget.Models.AzureAdConfig.cs
// This file's original implementation has been excluded from compilation to remove Azure traces.
#if false
namespace WileyWidget.Models;

/// <summary>
/// Lightweight configuration model that replaces the previously generated Azure AD settings class.
/// Provides defaults so the authentication service can gracefully handle missing configuration when
/// Azure integration is disabled.
/// </summary>
public sealed class AzureAdConfig
{
    private string _authority = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Authority
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_authority))
            {
                return _authority;
            }

            if (!string.IsNullOrWhiteSpace(TenantId))
            {
                return $"https://login.microsoftonline.com/{TenantId}";
            }

            return "https://login.microsoftonline.com/common";
        }
        set => _authority = value ?? string.Empty;
    }
}
#endif
