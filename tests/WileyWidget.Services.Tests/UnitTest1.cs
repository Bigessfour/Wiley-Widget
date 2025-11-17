using FluentAssertions;
using Xunit;

namespace WileyWidget.Services.Tests;

public class SampleTests
{
    [Fact]
    public void Truth_Is_True()
    {
        // Basic sanity test to verify test runner and project references
        true.Should().BeTrue();
    }
}
