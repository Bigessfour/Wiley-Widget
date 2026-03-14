using System.Collections.Generic;
using System.Linq;
using System.Text;
using WileyWidget.Models;

namespace WileyWidget.WinForms.Services.AI;

internal static class JarvisAttachmentPromptFormatter
{
    public static string NormalizePrompt(string? prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? "Please evaluate the attached files."
            : prompt.Trim();
    }

    public static string BuildDisplayPrompt(string prompt, IReadOnlyList<ChatPromptAttachment>? attachments)
    {
        var normalizedPrompt = NormalizePrompt(prompt);

        if (attachments == null || attachments.Count == 0)
        {
            return normalizedPrompt;
        }

        var builder = new StringBuilder();
        builder.Append(normalizedPrompt);
        builder.AppendLine();
        builder.AppendLine();
        builder.Append(attachments.Count == 1 ? "Attached file: " : "Attached files: ");
        builder.Append(string.Join(", ", attachments.Select(attachment => attachment.FileName)));
        return builder.ToString();
    }

    public static void AppendAttachmentContext(StringBuilder builder, IReadOnlyList<ChatPromptAttachment>? attachments)
    {
        if (attachments == null || attachments.Count == 0)
        {
            return;
        }

        builder.AppendLine("Attached file context for the current request:");

        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            builder.Append("[Attachment ");
            builder.Append(index + 1);
            builder.AppendLine("]");
            builder.Append("FileName: ");
            builder.AppendLine(attachment.FileName);
            builder.Append("ContentType: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(attachment.ContentType) ? "unknown" : attachment.ContentType);
            builder.Append("SizeBytes: ");
            builder.AppendLine(attachment.SizeBytes.ToString());
            builder.Append("Truncated: ");
            builder.AppendLine(attachment.IsTruncated ? "yes" : "no");
            builder.AppendLine("Content:");
            builder.AppendLine("```text");
            builder.AppendLine(attachment.Content);
            builder.AppendLine("```");
            builder.AppendLine();
        }
    }
}
