using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Services.Abstractions;

public sealed record AuthenticationSessionResult(
    string UserId,
    string DisplayName,
    string? Email,
    string Provider,
    bool IsDevelopmentBypass,
    IReadOnlyDictionary<string, string?> ProfileFields);

public interface IAuthenticationBootstrapper
{
    AuthenticationSessionResult? CurrentSession { get; }

    Task<AuthenticationSessionResult> EnsureAuthenticatedAsync(IWin32Window? ownerWindow, CancellationToken cancellationToken = default);
}
