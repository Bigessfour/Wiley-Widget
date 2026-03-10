using System;
using System.Collections.Generic;

namespace WileyWidget.Models;

/// <summary>
/// Structured export payload for branded report documents.
/// </summary>
public sealed record ReportExportDocument(
    string Title,
    string Subtitle,
    DateTime GeneratedAt,
    string GeneratedBy,
    ReportBrandingInfo Branding,
    IReadOnlyList<ReportExportSection> Sections,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Branding information rendered into report mastheads.
/// </summary>
public sealed record ReportBrandingInfo(
    string OrganizationName,
    string ApplicationName,
    string Attribution,
    string? LogoPath = null);

/// <summary>
/// Tabular section within an exported report document.
/// </summary>
public sealed record ReportExportSection(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);
