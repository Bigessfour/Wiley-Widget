using System;

namespace WileyWidget.Models
{
    /// <summary>
    /// Recent activity row for dashboard grid
    /// </summary>
    public record ActivityItem(
        string User,
        string Action,
        DateTime Date,
        decimal Amount,
        string Status,
        string Category
    );
}