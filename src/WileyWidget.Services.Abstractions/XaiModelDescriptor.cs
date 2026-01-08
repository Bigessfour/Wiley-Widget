namespace WileyWidget.Services.Abstractions
{
    using System.Collections.Generic;

    /// <summary>
    /// Lightweight descriptor for an xAI model as returned by the API.
    /// </summary>
    public sealed record XaiModelDescriptor(
        string Id,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string>? InputModalities,
        IReadOnlyList<string>? OutputModalities,
        long? CreatedUnix,
        string? Version);
}