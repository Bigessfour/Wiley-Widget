using System;
using FluentAssertions;
using WileyWidget.WinForms.Forms;
using Xunit;

namespace WileyWidget.WinForms.Tests.Integration.Forms;

[Trait("Category", "Integration")]
public sealed class SplashFormIntegrationTests
{
    [StaFact]
    public void Report_RaisesProgressChanged_InHeadlessMode()
    {
        var previous = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            using var splash = new SplashForm();
            SplashProgressChangedEventArgs? reported = null;
            splash.ProgressChanged += (_, args) => reported = args;

            splash.Report(0.25, "Loading", isIndeterminate: false);
            splash.Complete("Done");

            reported.Should().NotBeNull();
            reported!.Progress.Should().Be(0.25);
            reported.Message.Should().Be("Loading");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous);
        }
    }

    [StaFact]
    public void SplashForm_CanBeCreatedAndDisposed()
    {
        using var splash = new SplashForm();
        splash.Should().NotBeNull();
    }

    [StaFact]
    public void Report_HandlesIndeterminateProgress()
    {
        var previous = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            using var splash = new SplashForm();
            SplashProgressChangedEventArgs? reported = null;
            splash.ProgressChanged += (_, args) => reported = args;

            splash.Report(0.0, "Initializing", isIndeterminate: true);

            reported.Should().NotBeNull();
            reported!.IsIndeterminate.Should().BeTrue();
            reported.Message.Should().Be("Initializing");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous);
        }
    }

    [StaFact]
    public void Complete_SetsProgressToOneHundred()
    {
        var previous = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            using var splash = new SplashForm();
            SplashProgressChangedEventArgs? reported = null;
            splash.ProgressChanged += (_, args) => reported = args;

            splash.Complete("Finished");

            reported.Should().NotBeNull();
            reported!.Progress.Should().Be(1.0);
            reported.Message.Should().Be("Finished");
            reported.IsIndeterminate.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous);
        }
    }

    [StaFact]
    public void MultipleReports_WorkCorrectly()
    {
        var previous = Environment.GetEnvironmentVariable("WILEYWIDGET_UI_TESTS");

        try
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", "true");
            using var splash = new SplashForm();
            var reports = new System.Collections.Generic.List<SplashProgressChangedEventArgs>();
            splash.ProgressChanged += (_, args) => reports.Add(args);

            splash.Report(0.1, "Step 1");
            splash.Report(0.5, "Step 2", isIndeterminate: true);
            splash.Report(0.9, "Step 3");
            splash.Complete("Done");

            reports.Should().HaveCount(4);
            reports[0].Progress.Should().Be(0.1);
            reports[0].Message.Should().Be("Step 1");
            reports[0].IsIndeterminate.Should().BeFalse();

            reports[1].Progress.Should().Be(0.5);
            reports[1].IsIndeterminate.Should().BeTrue();

            reports[2].Progress.Should().Be(0.9);
            reports[3].Progress.Should().Be(1.0);
            reports[3].Message.Should().Be("Done");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WILEYWIDGET_UI_TESTS", previous);
        }
    }
}
