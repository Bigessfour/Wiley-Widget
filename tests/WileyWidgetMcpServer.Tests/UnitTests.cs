using Xunit;
using WileyWidget.McpServer.Tools;
using Moq;

namespace WileyWidgetMcpServer.Tests;

public class EvalCSharpToolTests
{
    [Fact]
    public void EvalCSharp_WithValidCode_ReturnsSuccess()
    {
        // Arrange
        var code = "Console.WriteLine(\"Test\"); return \"Success\";";

        // Act
        var result = EvalCSharpTool.EvalCSharp(code, jsonOutput: false);

        // Assert
        Assert.Contains("Success", result);
    }

    [Fact]
    public void EvalCSharp_WithInvalidCode_ReturnsError()
    {
        // Arrange
        var code = "invalid code syntax error";

        // Act
        var result = EvalCSharpTool.EvalCSharp(code, jsonOutput: false);

        // Assert
        Assert.Contains("Error", result);
    }
}

public class BatchValidateFormsToolTests
{
    [Fact]
    public void BatchValidateForms_WithNoForms_ReturnsTextReport()
    {
        // Arrange
        string[] formTypeNames = Array.Empty<string>();

        // Act
        var result = BatchValidateFormsTool.BatchValidateForms(formTypeNames, outputFormat: "text");

        // Assert
        Assert.Contains("BATCH FORM VALIDATION REPORT", result);
    }
}