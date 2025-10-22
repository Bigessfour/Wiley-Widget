using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using WileyWidget;
using Microsoft.Extensions.Configuration;

public class AppUtilitiesTests
{
    [Fact]
    public void ResolvePlaceholderRegex_MatchesPattern()
    {
        var regexField = typeof(WileyWidget.App).GetField("PlaceholderRegex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(regexField);
        var regex = regexField.GetValue(null) as Regex;
        Assert.NotNull(regex);
        Assert.Matches(regex!, "${VAR_NAME}");
    }

    [Fact]
    public void BuildConfiguration_LoadsDefaults()
    {
        var app = new TestableApp();
        var config = app.InvokeBuildConfiguration();
        Assert.NotNull(config);
        Assert.True(config.AsEnumerable().Any());
    }

    private class TestableApp : WileyWidget.App
    {
        public IConfiguration InvokeBuildConfiguration()
        {
            // BuildConfiguration is a private method; call via reflection
            var mi = typeof(WileyWidget.App).GetMethod("BuildConfiguration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (mi == null) throw new InvalidOperationException("BuildConfiguration method not found");
            return (IConfiguration)mi.Invoke(this, null)!;
        }
    }
}
