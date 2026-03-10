using System;
using WileyWidget.Models;
using WileyWidget.WinForms.Services.AI;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services.AI;

public sealed class JarvisAttachmentPromptFormatterTests
{
    [Fact]
    public void BuildDisplayPrompt_WithAttachmentsAndPrompt_IncludesAttachmentSummary()
    {
        var attachments = new[]
        {
            new ChatPromptAttachment { FileName = "startup.log" }
        };

        var prompt = JarvisAttachmentPromptFormatter.BuildDisplayPrompt("Summarize the issue", attachments);

        Assert.Contains("Summarize the issue", prompt, StringComparison.Ordinal);
        Assert.Contains("Attached file: startup.log", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDisplayPrompt_WithoutPrompt_UsesDefaultInstruction()
    {
        var prompt = JarvisAttachmentPromptFormatter.BuildDisplayPrompt(string.Empty, Array.Empty<ChatPromptAttachment>());

        Assert.Equal("Please evaluate the attached files.", prompt);
    }

    [Fact]
    public void AppendAttachmentContext_WritesAttachmentMetadataAndContent()
    {
        var builder = new System.Text.StringBuilder();
        var attachments = new[]
        {
            new ChatPromptAttachment
            {
                FileName = "errors.log",
                ContentType = "text/plain",
                SizeBytes = 512,
                Content = "Exception: boom",
                IsTruncated = true
            }
        };

        JarvisAttachmentPromptFormatter.AppendAttachmentContext(builder, attachments);

        var prompt = builder.ToString();
        Assert.Contains("Attached file context for the current request:", prompt, StringComparison.Ordinal);
        Assert.Contains("FileName: errors.log", prompt, StringComparison.Ordinal);
        Assert.Contains("ContentType: text/plain", prompt, StringComparison.Ordinal);
        Assert.Contains("SizeBytes: 512", prompt, StringComparison.Ordinal);
        Assert.Contains("Truncated: yes", prompt, StringComparison.Ordinal);
        Assert.Contains("Exception: boom", prompt, StringComparison.Ordinal);
    }
}