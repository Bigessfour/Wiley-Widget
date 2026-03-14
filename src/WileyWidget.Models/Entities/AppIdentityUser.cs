using System;
using Microsoft.AspNetCore.Identity;

namespace WileyWidget.Models.Entities;

public sealed class AppIdentityUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSignedInAtUtc { get; set; }
}