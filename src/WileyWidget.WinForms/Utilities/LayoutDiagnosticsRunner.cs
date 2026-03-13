using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.WinForms.DataGrid;
using Syncfusion.WinForms.Controls;
using ChartControl = Syncfusion.Windows.Forms.Chart.ChartControl;
using WileyWidget.Abstractions;
using WileyWidget.WinForms.Configuration;
using WileyWidget.WinForms.Controls.Base;
using WileyWidget.WinForms.Controls.Supporting;
using WileyWidget.WinForms.Helpers;
using WileyWidget.WinForms.Services;
using WileyWidget.WinForms.Themes;
using WinFormsLazyLoadViewModel = WileyWidget.WinForms.ViewModels.ILazyLoadViewModel;

namespace WileyWidget.WinForms.Utilities
{
    public sealed class LayoutDiagnosticsOptions
    {
        public Size LogicalClientSize { get; init; } = new(1400, 900);

        public float SimulatedScale { get; init; } = 1.25f;

        public string ThemeName { get; init; } = ThemeColors.DefaultTheme;

        public bool LoadRepresentativeData { get; init; } = true;

        public int StabilizationTimeoutMs { get; init; } = 2500;

        public int StabilitySampleCount { get; init; } = 3;

        public bool EnablePixelProof { get; init; } = true;

        public bool CapturePixelProofScreenshots { get; init; } = true;

        public string PixelProofScreenshotDirectory { get; init; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics");

        public int PixelProofSampleStride { get; init; } = 4;

        public int PixelProofBackgroundTolerance { get; init; } = 8;
    }

    public sealed class PanelLayoutIssue
    {
        public string Severity { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public string ControlName { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }

    public sealed class PanelPolishFinding
    {
        public string Severity { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Problem { get; init; } = string.Empty;

        public string UserImpact { get; init; } = string.Empty;

        public string Recommendation { get; init; } = string.Empty;

        public string Evidence { get; init; } = string.Empty;
    }

    public sealed class PanelChecklistItem
    {
        public string Section { get; init; } = string.Empty;

        public string CheckId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string Evidence { get; init; } = string.Empty;

        public string Recommendation { get; init; } = string.Empty;

        public string Source { get; init; } = "docs/WileyWidgetUIStandards.md";
    }

    public sealed class PanelChecklistSummary
    {
        public const string CurrentVersion = "2026-03-10";

        public string Version { get; init; } = CurrentVersion;

        public int Score { get; init; }

        public int Passed { get; init; }

        public int Failed { get; init; }

        public int ManualReview { get; init; }

        public int NotApplicable { get; init; }

        public string Summary { get; init; } = string.Empty;

        public IReadOnlyList<PanelChecklistItem> Items { get; init; } = Array.Empty<PanelChecklistItem>();
    }

    public sealed class TokenFootprintSummary
    {
        public string PatternName { get; init; } = string.Empty;

        public string CandidateTokens { get; init; } = string.Empty;

        public int MatchCount { get; init; }

        public long ApproxWhitespaceArea { get; init; }
    }

    public sealed class LayoutTokenRecommendation
    {
        public string TokenName { get; init; } = string.Empty;

        public string CurrentValue { get; init; } = string.Empty;

        public string RecommendedValue { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public string Evidence { get; init; } = string.Empty;
    }

    public sealed class PanelLayoutDiagnostic
    {
        public string PanelTypeName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string AnalysisSurfaceName { get; init; } = string.Empty;

        public bool Success { get; init; }

        public bool MeaningfulContentDetected { get; init; }

        public int VisualScore { get; init; }

        public string VisualSummary { get; init; } = string.Empty;

        public int PolishScore { get; init; }

        public string PolishSummary { get; init; } = string.Empty;

        public string? ScreenshotPath { get; init; }

        public Size ClientSize { get; init; }

        public int VisibleControlCount { get; init; }

        public int VisibleLeafControlCount { get; init; }

        public double TopLevelWhitespacePercent { get; init; }

        public double LeafWhitespacePercent { get; init; }

        public int LeftWhitespace { get; init; }

        public int TopWhitespace { get; init; }

        public int RightWhitespace { get; init; }

        public int BottomWhitespace { get; init; }

        public IReadOnlyList<TokenFootprintSummary> TokenFootprint { get; init; } = Array.Empty<TokenFootprintSummary>();

        public IReadOnlyList<PanelLayoutIssue> Issues { get; init; } = Array.Empty<PanelLayoutIssue>();

        public IReadOnlyList<PanelPolishFinding> VisualFindings { get; init; } = Array.Empty<PanelPolishFinding>();

        public IReadOnlyList<PanelPolishFinding> PolishFindings { get; init; } = Array.Empty<PanelPolishFinding>();

        public PanelChecklistSummary Checklist { get; init; } = new();

        public string? Error { get; init; }
    }

    public sealed class LayoutDiagnosticsReport
    {
        public DateTimeOffset GeneratedAtUtc { get; init; }

        public string ChecklistVersion { get; init; } = PanelChecklistSummary.CurrentVersion;

        public string ThemeName { get; init; } = string.Empty;

        public float SimulatedScale { get; init; }

        public Size LogicalClientSize { get; init; }

        public int PanelsAttempted { get; init; }

        public int PanelsSucceeded { get; init; }

        public int PanelsFailed { get; init; }

        public int PanelsMeaningful { get; init; }

        public int PanelsInconclusive { get; init; }

        public double AverageTopLevelWhitespacePercent { get; init; }

        public double AverageLeafWhitespacePercent { get; init; }

        public double AverageVisualScore { get; init; }

        public double AverageChecklistScore { get; init; }

        public int PanelsWithVisualFailures { get; init; }

        public int PanelsWithChecklistFailures { get; init; }

        public IReadOnlyList<TokenFootprintSummary> AggregateTokenFootprint { get; init; } = Array.Empty<TokenFootprintSummary>();

        public IReadOnlyList<LayoutTokenRecommendation> Recommendations { get; init; } = Array.Empty<LayoutTokenRecommendation>();

        public IReadOnlyList<PanelLayoutDiagnostic> Panels { get; init; } = Array.Empty<PanelLayoutDiagnostic>();
    }

    internal sealed class PixelProofCaptureResult
    {
        public string? ScreenshotPath { get; init; }

        public IReadOnlyList<PanelLayoutIssue> Issues { get; init; } = Array.Empty<PanelLayoutIssue>();
    }

    public static class LayoutDiagnosticsRunner
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private static readonly TokenPattern[] PaddingPatterns =
        {
            TokenPattern.ForPadding("PanelPadding", "LayoutTokens.PanelPadding", new Padding(LayoutTokens.PanelPadding)),
            TokenPattern.ForPadding("UniformPadding6", "LayoutTokens.ContentInnerPadding", LayoutTokens.ContentInnerPadding),
            TokenPattern.ForPadding("UniformPadding8", "LayoutTokens.PanelPaddingCompact", LayoutTokens.PanelPaddingCompact),
            TokenPattern.ForPadding("SectionPanelPadding", "LayoutTokens.SectionPanelPadding", LayoutTokens.SectionPanelPadding),
            TokenPattern.ForPadding("PanelPaddingTight", "LayoutTokens.PanelPaddingTight", LayoutTokens.PanelPaddingTight),
            TokenPattern.ForPadding("PanelPaddingSpacious", "LayoutTokens.PanelPaddingSpacious", LayoutTokens.PanelPaddingSpacious),
            TokenPattern.ForPadding("ToolbarPadding", "LayoutTokens.ToolbarPadding", LayoutTokens.ToolbarPadding),
        };

        private static readonly TokenPattern[] MarginPatterns =
        {
            TokenPattern.ForMargin("MetricCardMargin", "LayoutTokens.MetricCardMargin", LayoutTokens.MetricCardMargin),
            TokenPattern.ForScalarMargin("ContentMargin", "LayoutTokens.ContentMargin", LayoutTokens.ContentMargin),
            TokenPattern.ForScalarMargin("HalfContentMargin", "LayoutTokens.ContentMargin / 2", LayoutTokens.ContentMargin / 2),
        };

        private static readonly string[] FinanceDenseColumnTerms =
        {
            "amount",
            "actual",
            "account",
            "balance",
            "budget",
            "code",
            "date",
            "expense",
            "number",
            "payment",
            "revenue",
            "total",
            "variance",
        };


        private sealed record FieldThemeSnapshot(Control Control, string Category, string ThemeName, Color BackColor, Color ForeColor, string Signature);
        public static string RunAllPanelsAsJson()
        {
            return JsonSerializer.Serialize(RunAllPanels(), JsonOptions);
        }

        public static string RunAllPanelsAsJson(LayoutDiagnosticsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return JsonSerializer.Serialize(RunAllPanels(options), JsonOptions);
        }

        public static LayoutDiagnosticsReport RunAllPanels()
        {
            return RunAllPanels(new LayoutDiagnosticsOptions());
        }

        public static LayoutDiagnosticsReport RunAllPanels(LayoutDiagnosticsOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return RunOnSta(() => RunAllPanelsCore(options));
        }

        private static LayoutDiagnosticsReport RunAllPanelsCore(LayoutDiagnosticsOptions options)
        {
            using var _ = LayoutDiagnosticsMode.EnterScope();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var provider = DependencyInjection.ConfigureServices();
            SeedProgramServices(provider);
            InitializeSyncfusionRuntime(provider, options.ThemeName);
            var diagnosticsScope = provider.CreateScope();
            var diagnosticsServiceProvider = diagnosticsScope.ServiceProvider;

            var panelTypes = DiscoverPanelTypes().ToList();
            var diagnostics = new List<PanelLayoutDiagnostic>(panelTypes.Count);

            foreach (var panelType in panelTypes)
            {
                diagnostics.Add(AnalyzePanel(diagnosticsServiceProvider, panelType, options));
            }

            var successfulPanels = diagnostics.Where(panel => panel.Success).ToList();
            var meaningfulPanels = successfulPanels.Where(panel => panel.MeaningfulContentDetected).ToList();
            var aggregateFootprint = meaningfulPanels
                .SelectMany(panel => panel.TokenFootprint)
                .GroupBy(token => token.PatternName, StringComparer.Ordinal)
                .Select(group => new TokenFootprintSummary
                {
                    PatternName = group.Key,
                    CandidateTokens = group.First().CandidateTokens,
                    MatchCount = group.Sum(item => item.MatchCount),
                    ApproxWhitespaceArea = group.Sum(item => item.ApproxWhitespaceArea),
                })
                .OrderByDescending(token => token.ApproxWhitespaceArea)
                .ThenBy(token => token.PatternName, StringComparer.Ordinal)
                .ToList();

            return new LayoutDiagnosticsReport
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                ChecklistVersion = PanelChecklistSummary.CurrentVersion,
                ThemeName = options.ThemeName,
                SimulatedScale = options.SimulatedScale,
                LogicalClientSize = options.LogicalClientSize,
                PanelsAttempted = diagnostics.Count,
                PanelsSucceeded = successfulPanels.Count,
                PanelsFailed = diagnostics.Count - successfulPanels.Count,
                PanelsMeaningful = meaningfulPanels.Count,
                PanelsInconclusive = successfulPanels.Count - meaningfulPanels.Count,
                AverageTopLevelWhitespacePercent = Round(meaningfulPanels.Select(panel => panel.TopLevelWhitespacePercent).DefaultIfEmpty(0d).Average()),
                AverageLeafWhitespacePercent = Round(meaningfulPanels.Select(panel => panel.LeafWhitespacePercent).DefaultIfEmpty(0d).Average()),
                AverageVisualScore = Round(meaningfulPanels.Select(panel => (double)panel.VisualScore).DefaultIfEmpty(0d).Average()),
                AverageChecklistScore = Round(successfulPanels.Select(panel => (double)panel.Checklist.Score).DefaultIfEmpty(0d).Average()),
                PanelsWithVisualFailures = successfulPanels.Count(panel => panel.VisualFindings.Count > 0),
                PanelsWithChecklistFailures = successfulPanels.Count(panel => panel.Checklist.Failed > 0),
                AggregateTokenFootprint = aggregateFootprint,
                Recommendations = BuildRecommendations(aggregateFootprint),
                Panels = diagnostics
                    .OrderBy(panel => panel.MeaningfulContentDetected ? 1 : 0)
                    .ThenByDescending(panel => panel.LeafWhitespacePercent)
                    .ThenBy(panel => panel.DisplayName, StringComparer.Ordinal)
                    .ToList(),
            };
        }

        private static PanelLayoutDiagnostic AnalyzePanel(IServiceProvider serviceProvider, Type panelType, LayoutDiagnosticsOptions options)
        {
            Control? panel = null;
            Form? host = null;

            try
            {
                panel = CreatePanelInstance(serviceProvider, panelType);
                ConfigureSpecialHostPanels(serviceProvider, panel, panelType);
                host = CreateHost(options.LogicalClientSize);

                host.Controls.Add(panel);

                var pixelProof = PrepareForAnalysis(host, panel, panelType, options);

                var analysisSurface = GetAnalysisSurface(panel);
                var visibleControls = GetVisibleDescendants(analysisSurface).ToList();
                var visibleLeafControls = visibleControls.Where(IsLeafControl).ToList();

                var topLevelRects = GetContentRects(analysisSurface, visibleControls);
                var leafRects = GetContentRects(analysisSurface, visibleLeafControls);

                var clientArea = Math.Max(1L, (long)analysisSurface.ClientSize.Width * analysisSurface.ClientSize.Height);
                var topLevelArea = CalculateUnionArea(topLevelRects);
                var leafArea = CalculateUnionArea(leafRects);
                var topLevelEnvelope = GetEnvelope(topLevelRects);
                var meaningfulContentDetected = visibleControls.Count > 0 && topLevelArea > 0;
                var issues = FindIssues(panel, analysisSurface, options)
                    .Concat(pixelProof.Issues)
                    .Concat(FindStructuralIssues(panel, analysisSurface, visibleControls.Count, visibleLeafControls.Count, topLevelArea))
                    .Concat(FindPanelSpecificIssues(panelType, analysisSurface, options))
                    .ToList();
                var visualReview = BuildVisualReview(panelType, analysisSurface, issues, meaningfulContentDetected, options.SimulatedScale);
                var polishReview = BuildPolishReview(panelType, analysisSurface, issues, meaningfulContentDetected, options.SimulatedScale);
                var checklistReview = BuildChecklistReview(
                    panelType,
                    panel,
                    analysisSurface,
                    issues,
                    meaningfulContentDetected,
                    Round(100d * (clientArea - topLevelArea) / clientArea),
                    Round(100d * (clientArea - leafArea) / clientArea),
                    topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Width : Math.Max(0, topLevelEnvelope.Left),
                    topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Height : Math.Max(0, topLevelEnvelope.Top),
                    topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Width : Math.Max(0, analysisSurface.ClientSize.Width - topLevelEnvelope.Right),
                    topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Height : Math.Max(0, analysisSurface.ClientSize.Height - topLevelEnvelope.Bottom));
                var tokenFootprint = AnalyzeTokenFootprint(analysisSurface)
                    .Values
                    .OrderByDescending(token => token.ApproxWhitespaceArea)
                    .ThenBy(token => token.PatternName, StringComparer.Ordinal)
                    .ToList();

                return new PanelLayoutDiagnostic
                {
                    PanelTypeName = panelType.FullName ?? panelType.Name,
                    DisplayName = ResolveDisplayName(panelType),
                    AnalysisSurfaceName = GetControlIdentifier(analysisSurface),
                    Success = true,
                    MeaningfulContentDetected = meaningfulContentDetected,
                    VisualScore = visualReview.Score,
                    VisualSummary = visualReview.Summary,
                    PolishScore = polishReview.Score,
                    PolishSummary = polishReview.Summary,
                    ScreenshotPath = pixelProof.ScreenshotPath,
                    ClientSize = analysisSurface.ClientSize,
                    VisibleControlCount = visibleControls.Count,
                    VisibleLeafControlCount = visibleLeafControls.Count,
                    TopLevelWhitespacePercent = Round(100d * (clientArea - topLevelArea) / clientArea),
                    LeafWhitespacePercent = Round(100d * (clientArea - leafArea) / clientArea),
                    LeftWhitespace = topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Width : Math.Max(0, topLevelEnvelope.Left),
                    TopWhitespace = topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Height : Math.Max(0, topLevelEnvelope.Top),
                    RightWhitespace = topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Width : Math.Max(0, analysisSurface.ClientSize.Width - topLevelEnvelope.Right),
                    BottomWhitespace = topLevelEnvelope == Rectangle.Empty ? analysisSurface.ClientSize.Height : Math.Max(0, analysisSurface.ClientSize.Height - topLevelEnvelope.Bottom),
                    TokenFootprint = tokenFootprint,
                    Issues = issues,
                    VisualFindings = visualReview.Findings,
                    PolishFindings = polishReview.Findings,
                    Checklist = checklistReview,
                };
            }
            catch (Exception ex)
            {
                return new PanelLayoutDiagnostic
                {
                    PanelTypeName = panelType.FullName ?? panelType.Name,
                    DisplayName = ResolveDisplayName(panelType),
                    Success = false,
                    Error = ex.ToString(),
                    Issues = new[]
                    {
                        new PanelLayoutIssue
                        {
                            Severity = "error",
                            Kind = "instantiation",
                            ControlName = panelType.Name,
                            Message = ex.Message,
                        },
                    },
                };
            }
            finally
            {
                // Intentionally retain controls and the DI root for the remainder of the
                // diagnostics process. Many panels schedule deferred UI/async work via
                // BeginInvoke or timers, and disposing them here races those callbacks,
                // producing ObjectDisposedException against IServiceProvider.
                GC.KeepAlive(host);
                GC.KeepAlive(panel);
            }
        }

        private static PixelProofCaptureResult PrepareForAnalysis(Form host, Control panel, Type panelType, LayoutDiagnosticsOptions options)
        {
            host.AutoScaleMode = AutoScaleMode.Dpi;
            host.AutoScaleDimensions = new SizeF(96F, 96F);
            if (panel is ContainerControl containerPanel)
            {
                containerPanel.AutoScaleMode = AutoScaleMode.Dpi;
                containerPanel.AutoScaleDimensions = new SizeF(96F, 96F);
            }

            if (panel.MinimumSize.Width > host.ClientSize.Width || panel.MinimumSize.Height > host.ClientSize.Height)
            {
                host.ClientSize = new Size(
                    Math.Max(host.ClientSize.Width, panel.MinimumSize.Width),
                    Math.Max(host.ClientSize.Height, panel.MinimumSize.Height));
            }

            if (!string.IsNullOrWhiteSpace(options.ThemeName))
            {
                ThemeColors.EnsureThemeAssemblyLoadedForTheme(options.ThemeName);
                SfSkinManager.ApplicationVisualTheme = options.ThemeName;
                SfSkinManager.SetVisualStyle(host, options.ThemeName);
                SfSkinManager.SetVisualStyle(panel, options.ThemeName);
            }

            host.Show();
            host.CreateControl();
            panel.CreateControl();
            Application.DoEvents();

            if (options.SimulatedScale > 0f && Math.Abs(options.SimulatedScale - 1f) > 0.001f)
            {
                host.Scale(new SizeF(options.SimulatedScale, options.SimulatedScale));
            }

            RunLayoutPass(host, panel);

            if (options.LoadRepresentativeData)
            {
                TryPrimePanelContent(panel, options.StabilizationTimeoutMs);
            }

            WaitForVisualTreeStable(host, panel, options);
            RunLayoutPass(host, panel);
            ValidateSpecializedContentSurfaces(GetAnalysisSurface(panel));
            var pixelProof = CapturePixelProof(panel, panelType, options);
            RunLayoutPass(host, panel);
            return pixelProof;
        }

        private static void RunLayoutPass(Form host, Control panel)
        {
            host.PerformLayout();
            panel.PerformLayout();

            if (panel is ScopedPanelBase scopedPanel)
            {
                scopedPanel.TriggerForceFullLayout();
            }
            else
            {
                PerformLayoutRecursive(panel);
            }

            ApplyDeferredControlRefreshes(panel);

            host.Update();
            panel.Update();
            Application.DoEvents();
        }

        private static void ConfigureSpecialHostPanels(IServiceProvider serviceProvider, Control panel, Type panelType)
        {
            if (panel is not WileyWidget.WinForms.Controls.Panels.FormHostPanel formHostPanel)
            {
                return;
            }

            if (!string.Equals(ResolveDisplayName(panelType), "Rates", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (formHostPanel.HostedForm != null && !formHostPanel.HostedForm.IsDisposed)
            {
                return;
            }

            var ratesForm = ActivatorUtilities.CreateInstance<WileyWidget.WinForms.Forms.RatesPage>(serviceProvider);
            formHostPanel.HostForm(ratesForm);
        }

        private static void TryPrimePanelContent(Control panel, int timeoutMs)
        {
            var timeout = Math.Max(250, timeoutMs);
            var loadedViaPanelContract = false;

            if (panel is ICompletablePanel completablePanel)
            {
                WaitForTaskWithMessagePump(completablePanel.LoadAsync(CancellationToken.None), timeout);
                loadedViaPanelContract = true;
            }
            else if (panel is IAsyncInitializable asyncInitializable)
            {
                WaitForTaskWithMessagePump(asyncInitializable.InitializeAsync(CancellationToken.None), timeout);
                loadedViaPanelContract = true;
            }

            var viewModel = GetObjectProperty(panel, "ViewModel");
            if (!loadedViaPanelContract && viewModel is WinFormsLazyLoadViewModel lazyLoadViewModel && !lazyLoadViewModel.IsDataLoaded)
            {
                WaitForTaskWithMessagePump(lazyLoadViewModel.OnVisibilityChangedAsync(true), timeout);
            }
        }

        private static void WaitForTaskWithMessagePump(Task task, int timeoutMs)
        {
            if (task == null)
            {
                return;
            }

            var timeout = Math.Max(250, timeoutMs);
            var stopwatch = Stopwatch.StartNew();

            while (!task.IsCompleted && stopwatch.ElapsedMilliseconds < timeout)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }

            if (task.IsCompleted)
            {
                task.GetAwaiter().GetResult();
            }
        }

        private static void WaitForVisualTreeStable(Form host, Control panel, LayoutDiagnosticsOptions options)
        {
            var requiredStableSamples = Math.Max(2, options.StabilitySampleCount);
            var stableSamples = 0;
            var previousSnapshot = string.Empty;
            var timeout = Math.Max(250, options.StabilizationTimeoutMs);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeout)
            {
                RunLayoutPass(host, panel);
                var snapshot = CaptureLayoutSnapshot(panel);

                if (string.Equals(snapshot, previousSnapshot, StringComparison.Ordinal))
                {
                    stableSamples++;
                }
                else
                {
                    previousSnapshot = snapshot;
                    stableSamples = 1;
                }

                if (stableSamples >= requiredStableSamples)
                {
                    return;
                }

                Thread.Sleep(40);
            }
        }

        private static void ValidateSpecializedContentSurfaces(Control analysisSurface)
        {
            var webView = GetVisibleDescendants(analysisSurface)
                .FirstOrDefault(control => control.GetType().Name.Contains("BlazorWebView", StringComparison.Ordinal));
            if (webView != null)
            {
                webView.Invalidate(true);
                webView.Update();
                Application.DoEvents();

                if (GetObjectProperty(webView, "CoreWebView2") != null)
                {
                    TryInvokeNoArgMethod(webView, "Refresh");
                    TryInvokeNoArgMethod(webView, "Invalidate");
                }
            }

            var pdfViewer = GetVisibleDescendants(analysisSurface)
                .OfType<Syncfusion.Windows.Forms.PdfViewer.PdfViewerControl>()
                .FirstOrDefault();
            if (pdfViewer != null && pdfViewer.Visible)
            {
                pdfViewer.Invalidate(true);
                pdfViewer.Update();
                TryInvokeNoArgMethod(pdfViewer, "Refresh");
                Application.DoEvents();
            }

            foreach (var splitter in GetVisibleDescendants(analysisSurface)
                .OfType<Syncfusion.Windows.Forms.Tools.SplitContainerAdv>())
            {
                if (!splitter.IsHandleCreated)
                {
                    continue;
                }

                var oldDistance = splitter.SplitterDistance;
                var targetDistance = oldDistance + 50;
                if (!SafeSplitterDistanceHelper.TrySetSplitterDistance(splitter, targetDistance))
                {
                    continue;
                }

                splitter.PerformLayout();
                Application.DoEvents();
                SafeSplitterDistanceHelper.TrySetSplitterDistance(splitter, oldDistance);
                Application.DoEvents();
            }
        }

        private static PixelProofCaptureResult CapturePixelProof(Control panel, Type panelType, LayoutDiagnosticsOptions options)
        {
            if (!options.EnablePixelProof || !LayoutDiagnosticsMode.IsActive || panel.Width <= 0 || panel.Height <= 0)
            {
                return new PixelProofCaptureResult();
            }

            try
            {
                using var bitmap = new Bitmap(panel.Width, panel.Height);
                panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

                var screenshotPath = options.CapturePixelProofScreenshots
                    ? SavePixelProofScreenshot(bitmap, panelType, options)
                    : null;
                var issues = new List<PanelLayoutIssue>();
                var backgroundColor = SampleDominantBorderColor(bitmap, options.PixelProofSampleStride);

                DetectPanelHeaderClipping(panel, bitmap, backgroundColor, options, issues);
                DetectEmptyGraphicsSurfaces(panel, bitmap, backgroundColor, options, issues);

                return new PixelProofCaptureResult
                {
                    ScreenshotPath = screenshotPath,
                    Issues = issues,
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pixel proof failed for {panelType.Name}: {ex.Message}");
                return new PixelProofCaptureResult();
            }
        }

        private static string SavePixelProofScreenshot(Bitmap bitmap, Type panelType, LayoutDiagnosticsOptions options)
        {
            var directory = string.IsNullOrWhiteSpace(options.PixelProofScreenshotDirectory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics")
                : options.PixelProofScreenshotDirectory;
            Directory.CreateDirectory(directory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"{SanitizeFileNamePart(panelType.Name)}_{SanitizeFileNamePart(options.ThemeName)}_{options.SimulatedScale:0.##}x_{timestamp}.png";
            var screenshotPath = Path.Combine(directory, fileName);
            bitmap.Save(screenshotPath, ImageFormat.Png);
            return screenshotPath;
        }

        private static void DetectPanelHeaderClipping(Control panel, Bitmap bitmap, Color backgroundColor, LayoutDiagnosticsOptions options, ICollection<PanelLayoutIssue> issues)
        {
            var header = EnumerateControls(panel).OfType<PanelHeader>().FirstOrDefault(IsVisibleForLayout);
            if (header == null)
            {
                return;
            }

            var actionsPanel = header.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(IsVisibleForLayout);
            if (actionsPanel == null)
            {
                return;
            }

            var visibleActions = actionsPanel.Controls.Cast<Control>()
                .Where(IsVisibleForLayout)
                .ToList();
            if (visibleActions.Count == 0)
            {
                return;
            }

            var requiredWidth = actionsPanel.Padding.Horizontal + visibleActions.Sum(control => control.Width + control.Margin.Horizontal);
            var recommendedWidth = visibleActions.Count >= 3 ? LayoutTokens.GetScaled(280) : requiredWidth;
            var expectedWidth = Math.Max(requiredWidth, recommendedWidth);
            if (actionsPanel.Width >= expectedWidth)
            {
                return;
            }

            var actionsBounds = Rectangle.Intersect(new Rectangle(Point.Empty, panel.Size), GetBoundsRelativeToRoot(actionsPanel, panel));
            if (actionsBounds.Width <= 0 || actionsBounds.Height <= 0)
            {
                return;
            }

            var probeWidth = Math.Min(Math.Max(12, options.PixelProofSampleStride * 3), actionsBounds.Width);
            var probeRect = new Rectangle(actionsBounds.Right - probeWidth, actionsBounds.Top, probeWidth, actionsBounds.Height);
            var backgroundCoverage = CalculateBackgroundCoverage(bitmap, probeRect, backgroundColor, options.PixelProofSampleStride, options.PixelProofBackgroundTolerance);
            if (backgroundCoverage < 80d)
            {
                return;
            }

            issues.Add(new PanelLayoutIssue
            {
                Severity = "warning",
                Kind = "panel-header-clipping",
                ControlName = GetControlIdentifier(actionsPanel),
                Message = $"Panel header actions render at {actionsPanel.Width}px but need about {expectedWidth}px; the right edge of the action strip is {backgroundCoverage:0.#}% background pixels.",
            });
        }

        private static void DetectEmptyGraphicsSurfaces(Control panel, Bitmap bitmap, Color backgroundColor, LayoutDiagnosticsOptions options, ICollection<PanelLayoutIssue> issues)
        {
            var panelArea = Math.Max(1L, (long)panel.Width * panel.Height);

            foreach (var candidate in EnumerateControls(panel)
                .Where(IsVisibleForLayout)
                .Where(IsPixelProofSurfaceCandidate)
                .Select(control => new
                {
                    Control = control,
                    Bounds = Rectangle.Intersect(new Rectangle(Point.Empty, panel.Size), GetBoundsRelativeToRoot(control, panel)),
                })
                .Where(entry => entry.Bounds.Width >= 64 && entry.Bounds.Height >= 64)
                .Where(entry => ((long)entry.Bounds.Width * entry.Bounds.Height) >= panelArea / 8)
                .OrderByDescending(entry => (long)entry.Bounds.Width * entry.Bounds.Height)
                .Take(4))
            {
                var backgroundCoverage = CalculateBackgroundCoverage(bitmap, candidate.Bounds, backgroundColor, options.PixelProofSampleStride, options.PixelProofBackgroundTolerance);
                if (backgroundCoverage <= 65d)
                {
                    continue;
                }

                issues.Add(new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "empty-graphics-surface",
                    ControlName = GetControlIdentifier(candidate.Control),
                    Message = $"The rendered surface is {backgroundCoverage:0.#}% background pixels after stabilization, which suggests the graphics or preview content did not materialize.",
                });
            }
        }

        private static bool IsPixelProofSurfaceCandidate(Control control)
        {
            var identifier = GetControlIdentifier(control);
            var typeName = control.GetType().Name;

            return typeName.Contains("PdfViewer", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("WebView", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Chart", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("SplitContainer", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("reportviewer", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("preview", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("chart", StringComparison.OrdinalIgnoreCase);
        }

        private static double CalculateBackgroundCoverage(Bitmap bitmap, Rectangle region, Color backgroundColor, int sampleStride, int tolerance)
        {
            var clippedRegion = Rectangle.Intersect(new Rectangle(Point.Empty, bitmap.Size), region);
            if (clippedRegion.Width <= 0 || clippedRegion.Height <= 0)
            {
                return 0d;
            }

            var stride = Math.Max(1, sampleStride);
            var sampleCount = 0;
            var backgroundCount = 0;

            for (var y = clippedRegion.Top; y < clippedRegion.Bottom; y += stride)
            {
                for (var x = clippedRegion.Left; x < clippedRegion.Right; x += stride)
                {
                    sampleCount++;
                    if (IsWithinTolerance(bitmap.GetPixel(x, y), backgroundColor, tolerance))
                    {
                        backgroundCount++;
                    }
                }
            }

            return sampleCount == 0 ? 0d : 100d * backgroundCount / sampleCount;
        }

        private static Color SampleDominantBorderColor(Bitmap bitmap, int sampleStride)
        {
            var stride = Math.Max(1, sampleStride);
            var samples = new List<Color>();

            for (var x = 0; x < bitmap.Width; x += stride)
            {
                samples.Add(bitmap.GetPixel(x, 0));
                samples.Add(bitmap.GetPixel(x, bitmap.Height - 1));
            }

            for (var y = stride; y < bitmap.Height - 1; y += stride)
            {
                samples.Add(bitmap.GetPixel(0, y));
                samples.Add(bitmap.GetPixel(bitmap.Width - 1, y));
            }

            var dominantGroup = samples
                .GroupBy(color => (A: color.A / 8, R: color.R / 8, G: color.G / 8, B: color.B / 8))
                .OrderByDescending(group => group.Count())
                .FirstOrDefault();

            if (dominantGroup == null)
            {
                return bitmap.GetPixel(0, 0);
            }

            var averageA = (int)Math.Round(dominantGroup.Average(color => color.A));
            var averageR = (int)Math.Round(dominantGroup.Average(color => color.R));
            var averageG = (int)Math.Round(dominantGroup.Average(color => color.G));
            var averageB = (int)Math.Round(dominantGroup.Average(color => color.B));
            return Color.FromArgb(averageA, averageR, averageG, averageB);
        }

        private static bool IsWithinTolerance(Color sample, Color expected, int tolerance)
        {
            var channelTolerance = Math.Max(0, tolerance);
            return Math.Abs(sample.A - expected.A) <= channelTolerance
                && Math.Abs(sample.R - expected.R) <= channelTolerance
                && Math.Abs(sample.G - expected.G) <= channelTolerance
                && Math.Abs(sample.B - expected.B) <= channelTolerance;
        }

        private static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var characters = value.Trim()
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray();
            return new string(characters);
        }

        private static string CaptureLayoutSnapshot(Control panel)
        {
            var analysisSurface = GetAnalysisSurface(panel);
            var segments = EnumerateControls(analysisSurface)
                .Where(IsVisibleForLayout)
                .Select(control =>
                {
                    var bounds = ReferenceEquals(control, analysisSurface)
                        ? analysisSurface.ClientRectangle
                        : Rectangle.Intersect(analysisSurface.ClientRectangle, GetBoundsRelativeToRoot(control, analysisSurface));
                    return $"{GetControlIdentifier(control)}:{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
                })
                .OrderBy(segment => segment, StringComparer.Ordinal);

            return string.Join("|", segments.Prepend($"{analysisSurface.ClientSize.Width}x{analysisSurface.ClientSize.Height}"));
        }

        private static void ApplyDeferredControlRefreshes(Control root)
        {
            foreach (var control in EnumerateControls(root))
            {
                if (control is not SfDataGrid grid)
                {
                    continue;
                }

                TryInvokeNoArgMethod(GetObjectProperty(grid, "View"), "Refresh");
                TryInvokeNoArgMethod(grid, "UpdateScrollBars");
                grid.Refresh();
                grid.Update();
            }
        }

        private static void TryInvokeNoArgMethod(object? instance, string methodName)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, null);
            }
            catch
            {
                // Best-effort only. Diagnostics should continue even if a deferred refresh hook is unavailable.
            }
        }

        private static Control CreatePanelInstance(IServiceProvider serviceProvider, Type panelType)
        {
            var instance = ActivatorUtilities.CreateInstance(serviceProvider, panelType) as Control;
            if (instance == null)
            {
                throw new InvalidOperationException($"Unable to create panel instance for {panelType.FullName}.");
            }

            return instance;
        }

        private static Form CreateHost(Size logicalClientSize)
        {
            return new Form
            {
                AutoScaleMode = AutoScaleMode.Dpi,
                AutoScaleDimensions = new SizeF(96F, 96F),
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                ClientSize = logicalClientSize,
            };
        }

        private static Control GetAnalysisSurface(Control root)
        {
            var contentHost = FindDescendantByName(root, "ScopedPanelContentHost");
            if (contentHost != null && IsVisibleForLayout(contentHost) && GetVisibleDescendants(contentHost).Any())
            {
                return contentHost;
            }

            var namedContentChild = root.Controls.Cast<Control>()
                .Where(IsVisibleForLayout)
                .Where(control => !string.Equals(control.Name, "ScopedPanelContentHost", StringComparison.Ordinal))
                .Where(control => control.Name.Contains("Content", StringComparison.OrdinalIgnoreCase))
                .Where(control => control is not StatusStrip and not MenuStrip)
                .Where(control => !IsOverlayLike(control))
                .OrderByDescending(control => control.Width * control.Height)
                .FirstOrDefault();

            if (namedContentChild != null)
            {
                return namedContentChild;
            }

            var largestVisibleChild = root.Controls.Cast<Control>()
                .Where(IsVisibleForLayout)
                .Where(control => !string.Equals(control.Name, "ScopedPanelContentHost", StringComparison.Ordinal))
                .Where(control => control is not StatusStrip and not MenuStrip)
                .Where(control => !control.GetType().Name.Contains("Header", StringComparison.OrdinalIgnoreCase))
                .Where(control => !IsOverlayLike(control))
                .OrderByDescending(control => control.Width * control.Height)
                .FirstOrDefault();

            return largestVisibleChild ?? root;
        }

        private static Control? FindDescendantByName(Control root, string name)
        {
            foreach (Control child in root.Controls)
            {
                if (string.Equals(child.Name, name, StringComparison.Ordinal))
                {
                    return child;
                }

                var match = FindDescendantByName(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static IEnumerable<Type> DiscoverPanelTypes()
        {
            var registryTypes = PanelRegistry
                .GetAllRegisteredPanels(includeHidden: true)
                .Select(entry => entry.PanelType);

            var assemblyTypes = typeof(ScopedPanelBase).Assembly
                .GetTypes()
                .Where(type => type.IsClass
                    && !type.IsAbstract
                    && typeof(Control).IsAssignableFrom(type)
                    && type.Name.EndsWith("Panel", StringComparison.Ordinal)
                    && type.Namespace != null
                    && (type.Namespace.StartsWith("WileyWidget.WinForms.Controls.Panels", StringComparison.Ordinal)
                        || type == typeof(CsvMappingWizardPanel)));

            return registryTypes
                .Concat(assemblyTypes)
                .Distinct()
                .OrderBy(type => ResolveDisplayName(type), StringComparer.Ordinal)
                .ThenBy(type => type.FullName, StringComparer.Ordinal);
        }

        private static string ResolveDisplayName(Type panelType)
        {
            var registryEntry = PanelRegistry.Panels.FirstOrDefault(entry => entry.PanelType == panelType);
            if (registryEntry != null)
            {
                return registryEntry.DisplayName;
            }

            return panelType.Name.EndsWith("Panel", StringComparison.Ordinal)
                ? panelType.Name[..^"Panel".Length]
                : panelType.Name;
        }

        private static IEnumerable<Control> GetVisibleDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (!IsVisibleForLayout(child))
                {
                    continue;
                }

                yield return child;

                foreach (var descendant in GetVisibleDescendants(child))
                {
                    yield return descendant;
                }
            }
        }

        private static List<Rectangle> GetContentRects(Control analysisSurface, IEnumerable<Control> controls)
        {
            var rects = new List<Rectangle>();
            var surfaceArea = Math.Max(1L, (long)analysisSurface.ClientSize.Width * analysisSurface.ClientSize.Height);

            foreach (var control in controls)
            {
                if (!IsVisibleForLayout(control) || ShouldIgnoreForContentArea(control))
                {
                    continue;
                }

                var bounds = Rectangle.Intersect(analysisSurface.ClientRectangle, GetBoundsRelativeToRoot(control, analysisSurface));
                if (bounds.IsEmpty || bounds.Width <= 3 || bounds.Height <= 3)
                {
                    continue;
                }

                if (IsOverlayLike(control))
                {
                    var coverage = 100d * ((long)bounds.Width * bounds.Height) / surfaceArea;
                    if (coverage >= 25d)
                    {
                        continue;
                    }
                }

                rects.Add(bounds);
            }

            return rects;
        }

        private static bool IsVisibleForLayout(Control control)
        {
            return control.Visible && control.Width > 0 && control.Height > 0;
        }

        private static bool IsLeafControl(Control control)
        {
            return !control.Controls.Cast<Control>().Any(IsVisibleForLayout);
        }

        private static Rectangle GetBoundsRelativeToRoot(Control control, Control root)
        {
            if (ReferenceEquals(control, root))
            {
                return root.ClientRectangle;
            }

            var rectangle = new Rectangle(control.Location, control.Size);
            var current = control.Parent;

            while (current != null && !ReferenceEquals(current, root))
            {
                rectangle.Offset(current.Left, current.Top);
                current = current.Parent;
            }

            return current == null ? Rectangle.Empty : rectangle;
        }

        private static long CalculateUnionArea(IReadOnlyCollection<Rectangle> rectangles)
        {
            if (rectangles.Count == 0)
            {
                return 0;
            }

            var normalized = rectangles
                .Where(rect => rect.Width > 0 && rect.Height > 0)
                .Select(rect => Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom))
                .ToList();

            if (normalized.Count == 0)
            {
                return 0;
            }

            var xCoordinates = normalized
                .SelectMany(rect => new[] { rect.Left, rect.Right })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            long area = 0;

            for (var index = 0; index < xCoordinates.Length - 1; index++)
            {
                var x1 = xCoordinates[index];
                var x2 = xCoordinates[index + 1];
                var width = x2 - x1;

                if (width <= 0)
                {
                    continue;
                }

                var intervals = normalized
                    .Where(rect => rect.Left < x2 && rect.Right > x1)
                    .Select(rect => (Start: rect.Top, End: rect.Bottom))
                    .OrderBy(interval => interval.Start)
                    .ToList();

                if (intervals.Count == 0)
                {
                    continue;
                }

                var unionHeight = 0;
                var currentStart = intervals[0].Start;
                var currentEnd = intervals[0].End;

                for (var intervalIndex = 1; intervalIndex < intervals.Count; intervalIndex++)
                {
                    var interval = intervals[intervalIndex];
                    if (interval.Start <= currentEnd)
                    {
                        currentEnd = Math.Max(currentEnd, interval.End);
                        continue;
                    }

                    unionHeight += currentEnd - currentStart;
                    currentStart = interval.Start;
                    currentEnd = interval.End;
                }

                unionHeight += currentEnd - currentStart;
                area += (long)width * unionHeight;
            }

            return area;
        }

        private static Rectangle GetEnvelope(IReadOnlyCollection<Rectangle> rectangles)
        {
            if (rectangles.Count == 0)
            {
                return Rectangle.Empty;
            }

            var validRects = rectangles.Where(rect => rect.Width > 0 && rect.Height > 0).ToList();
            if (validRects.Count == 0)
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(
                validRects.Min(rect => rect.Left),
                validRects.Min(rect => rect.Top),
                validRects.Max(rect => rect.Right),
                validRects.Max(rect => rect.Bottom));
        }

        private static IReadOnlyDictionary<string, TokenFootprintSummary> AnalyzeTokenFootprint(Control root)
        {
            var result = new Dictionary<string, MutableTokenFootprint>(StringComparer.Ordinal);
            var allControls = new[] { root }.Concat(GetVisibleDescendants(root));

            foreach (var control in allControls)
            {
                foreach (var pattern in PaddingPatterns)
                {
                    if (!pattern.IsMatch(control.Padding))
                    {
                        continue;
                    }

                    var whitespaceArea = EstimatePaddingWhitespaceArea(control);
                    if (whitespaceArea <= 0)
                    {
                        continue;
                    }

                    AddTokenFootprint(result, pattern, whitespaceArea);
                }

                foreach (var pattern in MarginPatterns)
                {
                    if (!pattern.IsMatch(control.Margin))
                    {
                        continue;
                    }

                    var whitespaceArea = EstimateMarginWhitespaceArea(control);
                    if (whitespaceArea <= 0)
                    {
                        continue;
                    }

                    AddTokenFootprint(result, pattern, whitespaceArea);
                }
            }

            return result.ToDictionary(
                pair => pair.Key,
                pair => new TokenFootprintSummary
                {
                    PatternName = pair.Value.PatternName,
                    CandidateTokens = pair.Value.CandidateTokens,
                    MatchCount = pair.Value.MatchCount,
                    ApproxWhitespaceArea = pair.Value.ApproxWhitespaceArea,
                },
                StringComparer.Ordinal);
        }

        private static void AddTokenFootprint(IDictionary<string, MutableTokenFootprint> result, TokenPattern pattern, long whitespaceArea)
        {
            if (!result.TryGetValue(pattern.PatternName, out var aggregate))
            {
                aggregate = new MutableTokenFootprint(pattern.PatternName, pattern.CandidateTokens);
                result[pattern.PatternName] = aggregate;
            }

            aggregate.MatchCount++;
            aggregate.ApproxWhitespaceArea += whitespaceArea;
        }

        private static long EstimatePaddingWhitespaceArea(Control control)
        {
            if (control.ClientSize.Width <= 0 || control.ClientSize.Height <= 0)
            {
                return 0;
            }

            var innerWidth = Math.Max(0, control.ClientSize.Width - control.Padding.Left - control.Padding.Right);
            var innerHeight = Math.Max(0, control.ClientSize.Height - control.Padding.Top - control.Padding.Bottom);
            var totalArea = (long)control.ClientSize.Width * control.ClientSize.Height;
            var innerArea = (long)innerWidth * innerHeight;
            return Math.Max(0, totalArea - innerArea);
        }

        private static long EstimateMarginWhitespaceArea(Control control)
        {
            if (!IsVisibleForLayout(control))
            {
                return 0;
            }

            var outerWidth = control.Width + control.Margin.Left + control.Margin.Right;
            var outerHeight = control.Height + control.Margin.Top + control.Margin.Bottom;
            if (outerWidth <= 0 || outerHeight <= 0)
            {
                return 0;
            }

            var outerArea = (long)outerWidth * outerHeight;
            var innerArea = (long)control.Width * control.Height;
            return Math.Max(0, outerArea - innerArea);
        }

        private static IEnumerable<PanelLayoutIssue> FindIssues(Control root, Control analysisSurface, LayoutDiagnosticsOptions options)
        {
            foreach (var issue in FindClippedControls(analysisSurface))
            {
                yield return issue;
            }

            foreach (var issue in FindOverlayIssues(analysisSurface))
            {
                yield return issue;
            }

            foreach (var issue in FindTextFitIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindGridHeaderIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindGridUsabilityIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindChartIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindInteractiveSizingIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindAlignmentIssues(analysisSurface, options.SimulatedScale))
            {
                yield return issue;
            }

            foreach (var issue in FindThemeIssues(root, options.ThemeName))
            {
                yield return issue;
            }

            foreach (var issue in FindBaselineConformanceIssues(root, analysisSurface))
            {
                yield return issue;
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindBaselineConformanceIssues(Control root, Control analysisSurface)
        {
            if (root is not WileyWidget.WinForms.Controls.Panels.FormHostPanel && !HasVisiblePanelHeader(root))
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "panel-header",
                    ControlName = GetControlIdentifier(root),
                    Message = "No visible PanelHeader was detected on the panel surface.",
                };
            }

            if (root is ContainerControl container && container.AutoScaleMode != AutoScaleMode.Dpi)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "autoscale-mode",
                    ControlName = GetControlIdentifier(root),
                    Message = $"AutoScaleMode is {container.AutoScaleMode} instead of Dpi.",
                };
            }

            if (root.Dock != DockStyle.Fill)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "root-dock",
                    ControlName = GetControlIdentifier(root),
                    Message = $"Root panel Dock is {root.Dock} instead of Fill.",
                };
            }

            var minimumSize = root.MinimumSize;
            if (minimumSize.Width <= 0 || minimumSize.Height <= 0)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "minimum-size",
                    ControlName = GetControlIdentifier(root),
                    Message = "Root panel does not declare a minimum size.",
                };
            }
            else if (minimumSize.Width < 960 || minimumSize.Height < 600)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "minimum-size",
                    ControlName = GetControlIdentifier(root),
                    Message = $"MinimumSize {minimumSize.Width}x{minimumSize.Height}px is below the repo baseline for panel usability.",
                };
            }
            else if (minimumSize.Width < 1024 || minimumSize.Height < 720)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "info",
                    Kind = "minimum-size",
                    ControlName = GetControlIdentifier(root),
                    Message = $"MinimumSize {minimumSize.Width}x{minimumSize.Height}px is usable but below the preferred 1024x720 docked baseline.",
                };
            }

            var unnamedInteractiveControls = FindInteractiveControlsMissingAccessibleNames(analysisSurface).ToList();
            if (unnamedInteractiveControls.Count > 0)
            {
                var examples = string.Join(", ", unnamedInteractiveControls.Take(4).Select(GetControlIdentifier));
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "accessible-name",
                    ControlName = GetControlIdentifier(analysisSurface),
                    Message = $"{unnamedInteractiveControls.Count} interactive controls are missing AccessibleName. Examples: {examples}.",
                };
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindTextFitIssues(Control root, float simulatedScale)
        {
            foreach (var control in EnumerateControls(root))
            {
                if (!IsVisibleForLayout(control) || ShouldIgnoreUtilityControl(control) || !ShouldInspectTextFit(control))
                {
                    continue;
                }

                var text = GetDisplayedText(control);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var availableBounds = GetTextBounds(control);
                if (availableBounds.Width <= 0 || availableBounds.Height <= 0)
                {
                    continue;
                }

                var font = control.Font ?? Control.DefaultFont;
                var singleLineSize = MeasureTextSingleLine(text, font);
                var wrappedSize = MeasureTextForBounds(text, font, Math.Max(1, availableBounds.Width));
                var widthShortfall = singleLineSize.Width - availableBounds.Width;
                var heightShortfall = wrappedSize.Height - availableBounds.Height;

                if (text.Contains("…", StringComparison.Ordinal) || text.Contains("...", StringComparison.Ordinal))
                {
                    var isTransientOverlayText = ShouldTreatAsTransientOverlayText(control, text);
                    yield return new PanelLayoutIssue
                    {
                        Severity = isTransientOverlayText ? "info" : "warning",
                        Kind = isTransientOverlayText ? "overlay-placeholder-text" : "text-truncation",
                        ControlName = GetControlIdentifier(control),
                        Message = isTransientOverlayText
                            ? $"Transient overlay text contains an ellipsis at scale {simulatedScale:0.##}: '{text}'."
                            : $"Displayed text already contains an ellipsis at scale {simulatedScale:0.##}: '{text}'.",
                    };
                    continue;
                }

                if (!text.Contains(Environment.NewLine, StringComparison.Ordinal) && !text.Contains('\n') && widthShortfall > 6)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = widthShortfall > 30 ? "warning" : "info",
                        Kind = "text-truncation",
                        ControlName = GetControlIdentifier(control),
                        Message = $"Text '{text}' needs about {singleLineSize.Width}px but only {availableBounds.Width}px is available at scale {simulatedScale:0.##}.",
                    };
                }

                if (IsSingleLineTextControl(control) && heightShortfall > 4)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = "warning",
                        Kind = "text-clipping",
                        ControlName = GetControlIdentifier(control),
                        Message = $"Text '{text}' needs about {wrappedSize.Height}px of height but only {availableBounds.Height}px is available at scale {simulatedScale:0.##}.",
                    };
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindGridHeaderIssues(Control root, float simulatedScale)
        {
            foreach (var grid in EnumerateControls(root).OfType<SfDataGrid>())
            {
                using var headerFont = new Font(grid.Font ?? Control.DefaultFont, FontStyle.Bold);
                foreach (var column in grid.Columns.OfType<GridColumnBase>())
                {
                    var headerText = string.IsNullOrWhiteSpace(column.HeaderText) ? column.MappingName : column.HeaderText;
                    if (string.IsNullOrWhiteSpace(headerText))
                    {
                        continue;
                    }

                    var columnWidth = GetGridColumnWidth(column);
                    if (columnWidth <= 0)
                    {
                        continue;
                    }

                    var headerHeight = Math.Max(1, grid.HeaderRowHeight);
                    var availableWidth = Math.Max(1, columnWidth - 28);
                    var requiredWidth = TextRenderer.MeasureText(headerText, headerFont, new Size(int.MaxValue, headerHeight), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                    var requiredHeight = TextRenderer.MeasureText(headerText, headerFont, new Size(availableWidth, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.WordBreak).Height;

                    if (headerText.Contains("…", StringComparison.Ordinal) || headerText.Contains("...", StringComparison.Ordinal) || requiredWidth > availableWidth + 4)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "warning",
                            Kind = "grid-header-truncation",
                            ControlName = $"{GetControlIdentifier(grid)}.{column.MappingName}",
                            Message = $"Header '{headerText}' needs about {requiredWidth}px but column width only leaves {availableWidth}px at scale {simulatedScale:0.##}.",
                        };
                    }

                    if (requiredHeight > headerHeight - 4)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "grid-header-height",
                            ControlName = $"{GetControlIdentifier(grid)}.{column.MappingName}",
                            Message = $"Header '{headerText}' is tight vertically: row height {headerHeight}px versus about {requiredHeight}px needed at scale {simulatedScale:0.##}.",
                        };
                    }
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindGridUsabilityIssues(Control root, float simulatedScale)
        {
            foreach (var grid in EnumerateControls(root).OfType<SfDataGrid>())
            {
                var rowHeight = Math.Max(1, grid.RowHeight);
                var fontHeight = (grid.Font ?? Control.DefaultFont).Height;
                var recommendedRowHeight = Math.Max(32, (int)Math.Ceiling(fontHeight * 1.4));
                if (rowHeight < recommendedRowHeight)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = "warning",
                        Kind = "grid-row-height",
                        ControlName = GetControlIdentifier(grid),
                        Message = $"Grid row height is {rowHeight}px but about {recommendedRowHeight}px is safer for the current font at scale {simulatedScale:0.##}.",
                    };
                }

                foreach (var column in grid.Columns.OfType<GridColumnBase>())
                {
                    var headerText = string.IsNullOrWhiteSpace(column.HeaderText) ? column.MappingName : column.HeaderText;
                    var columnWidth = GetGridColumnWidth(column);
                    if (string.IsNullOrWhiteSpace(headerText) || columnWidth <= 0)
                    {
                        continue;
                    }

                    var minimumComfortableWidth = LooksLikeDenseFinanceColumn(headerText, column.MappingName) ? 88 : 60;
                    if (columnWidth < minimumComfortableWidth)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = columnWidth <= minimumComfortableWidth - 12 ? "warning" : "info",
                            Kind = "grid-column-width",
                            ControlName = $"{GetControlIdentifier(grid)}.{column.MappingName}",
                            Message = $"Column '{headerText}' is only {columnWidth}px wide; finance-style values are likely to feel cramped below {minimumComfortableWidth}px at scale {simulatedScale:0.##}.",
                        };
                    }
                }

                if (HasVisibleHorizontalScrollbar(grid))
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = "info",
                        Kind = "grid-horizontal-scroll",
                        ControlName = GetControlIdentifier(grid),
                        Message = $"A visible horizontal scrollbar is active at scale {simulatedScale:0.##}; the working grid surface is already width-constrained.",
                    };
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindChartIssues(Control root, float simulatedScale)
        {
            foreach (var chart in EnumerateControls(root).OfType<ChartControl>())
            {
                if (!IsVisibleForLayout(chart))
                {
                    continue;
                }

                var xAxis = GetObjectProperty(chart, "PrimaryXAxis");
                var yAxis = GetObjectProperty(chart, "PrimaryYAxis");
                var xAxisTitle = GetStringProperty(xAxis, "Title");
                var yAxisTitle = GetStringProperty(yAxis, "Title");
                var hasAxisTitles = !string.IsNullOrWhiteSpace(xAxisTitle) || !string.IsNullOrWhiteSpace(yAxisTitle);
                var hasRotatedXLabels = GetBoolProperty(xAxis, "LabelRotate") || GetDoubleProperty(xAxis, "LabelRotateAngle") >= 30d;
                var elementsSpacing = GetDoubleProperty(chart, "ElementsSpacing");
                var legend = GetObjectProperty(chart, "Legend");
                var hasLegend = GetBoolProperty(chart, "ShowLegend") || GetBoolProperty(legend, "Visible");
                var legendPosition = GetStringProperty(legend, "Position");
                var horizontalLegend = string.Equals(legendPosition, "Top", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(legendPosition, "Bottom", StringComparison.OrdinalIgnoreCase);
                var verticalLegend = string.Equals(legendPosition, "Left", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(legendPosition, "Right", StringComparison.OrdinalIgnoreCase);

                if (chart.Width < 360 || chart.Height < 240)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = chart.Width < 320 || chart.Height < 210 ? "warning" : "info",
                        Kind = "chart-surface-sizing",
                        ControlName = GetControlIdentifier(chart),
                        Message = $"Chart surface is only {chart.Width}x{chart.Height}px at scale {simulatedScale:0.##}; axis labels and finance values may feel compressed.",
                    };
                }

                if ((hasAxisTitles || hasRotatedXLabels) && elementsSpacing > 0 && elementsSpacing < 8)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = elementsSpacing <= 4 ? "warning" : "info",
                        Kind = "chart-axis-padding",
                        ControlName = GetControlIdentifier(chart),
                        Message = $"Chart elements spacing is only {elementsSpacing:0.#}px while titled or rotated axes are active at scale {simulatedScale:0.##}.",
                    };
                }

                if (hasLegend && hasAxisTitles)
                {
                    var requiredWidth = verticalLegend ? 500 : 400;
                    var requiredHeight = (horizontalLegend || hasRotatedXLabels) ? 320 : 280;
                    if (chart.Width < requiredWidth || chart.Height < requiredHeight)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = chart.Width < requiredWidth - 40 || chart.Height < requiredHeight - 30 ? "warning" : "info",
                            Kind = "chart-legend-crowding",
                            ControlName = GetControlIdentifier(chart),
                            Message = $"Chart is using axis titles and a {legendPosition.ToLowerInvariant()} legend on a {chart.Width}x{chart.Height}px surface; layouts below about {requiredWidth}x{requiredHeight}px tend to crowd titles, legends, and the plot area at scale {simulatedScale:0.##}.",
                        };
                    }
                }

                if (hasRotatedXLabels && chart.Height < 260)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = chart.Height < 220 ? "warning" : "info",
                        Kind = "chart-axis-label-fit",
                        ControlName = GetControlIdentifier(chart),
                        Message = $"Chart uses rotated X-axis labels but only has {chart.Height}px of height at scale {simulatedScale:0.##}.",
                    };
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindInteractiveSizingIssues(Control root, float simulatedScale)
        {
            foreach (var control in EnumerateControls(root))
            {
                if (!IsVisibleForLayout(control) || ShouldIgnoreUtilityControl(control))
                {
                    continue;
                }

                if (IsButtonLike(control))
                {
                    if (control.Height < 32)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "warning",
                            Kind = "control-sizing",
                            ControlName = GetControlIdentifier(control),
                            Message = $"Button height is only {control.Height}px at scale {simulatedScale:0.##}; this is below a comfortable click target for repeated use.",
                        };
                    }

                    if (control.Width < 96)
                    {
                        if (control.Width < 40)
                        {
                            continue;
                        }

                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "control-sizing",
                            ControlName = GetControlIdentifier(control),
                            Message = $"Button width is only {control.Width}px at scale {simulatedScale:0.##}; captions and icons are likely to feel cramped.",
                        };
                    }
                }

                if (IsComboLike(control))
                {
                    if (control.Height < 32)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "warning",
                            Kind = "control-sizing",
                            ControlName = GetControlIdentifier(control),
                            Message = $"Selector height is only {control.Height}px at scale {simulatedScale:0.##}; this is tight for daily data-entry use.",
                        };
                    }

                    if (control.Width < 160)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "control-sizing",
                            ControlName = GetControlIdentifier(control),
                            Message = $"Selector width is only {control.Width}px at scale {simulatedScale:0.##}; typical values may not display comfortably.",
                        };
                    }
                }

                if (LooksLikeSummaryCard(control) && (control.Width < 180 || control.Height < 88))
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = control.Width < 160 || control.Height < 76 ? "warning" : "info",
                        Kind = "summary-card-sizing",
                        ControlName = GetControlIdentifier(control),
                        Message = $"Summary card size {control.Width}x{control.Height}px is tight for KPI scanning at scale {simulatedScale:0.##}.",
                    };
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindAlignmentIssues(Control root, float simulatedScale)
        {
            foreach (var flow in EnumerateControls(root).OfType<FlowLayoutPanel>())
            {
                var children = flow.Controls.Cast<Control>()
                    .Where(IsVisibleForLayout)
                    .OrderBy(control => control.Top)
                    .ThenBy(control => control.Left)
                    .ToList();

                if (children.Count < 3)
                {
                    continue;
                }

                var firstRow = children.Where(control => Math.Abs(control.Top - children[0].Top) <= 4).OrderBy(control => control.Left).ToList();
                if (firstRow.Count >= 3)
                {
                    var gaps = new List<int>();
                    for (var index = 1; index < firstRow.Count; index++)
                    {
                        gaps.Add(firstRow[index].Left - firstRow[index - 1].Right);
                    }

                    if (gaps.Count >= 2 && gaps.Max() - gaps.Min() > 4)
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "alignment-spacing",
                            ControlName = GetControlIdentifier(flow),
                            Message = $"Sibling gaps vary from {gaps.Min()}px to {gaps.Max()}px at scale {simulatedScale:0.##}; the row will read as uneven.",
                        };
                    }

                    var widths = firstRow.Select(control => control.Width).ToList();
                    if (widths.Max() - widths.Min() > 8 && firstRow.All(LooksLikeSummaryCard))
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "alignment-card-balance",
                            ControlName = GetControlIdentifier(flow),
                            Message = $"Summary card widths vary from {widths.Min()}px to {widths.Max()}px at scale {simulatedScale:0.##}; KPI cards will not feel evenly balanced.",
                        };
                    }
                }
            }

            foreach (var table in EnumerateControls(root).OfType<TableLayoutPanel>())
            {
                var visibleChildren = table.Controls.Cast<Control>().Where(IsVisibleForLayout).ToList();
                if (visibleChildren.Count < 2)
                {
                    continue;
                }

                foreach (var row in visibleChildren.GroupBy(control => table.GetPositionFromControl(control).Row))
                {
                    var rowControls = row.OrderBy(control => control.Left).ToList();
                    if (rowControls.Count < 2)
                    {
                        continue;
                    }

                    var topVariance = rowControls.Max(control => control.Top) - rowControls.Min(control => control.Top);
                    var centerVariance = rowControls.Max(control => control.Top + (control.Height / 2)) - rowControls.Min(control => control.Top + (control.Height / 2));
                    if (centerVariance > 4 || (topVariance > 8 && centerVariance > 0))
                    {
                        yield return new PanelLayoutIssue
                        {
                            Severity = "info",
                            Kind = "alignment-row",
                            ControlName = GetControlIdentifier(table),
                            Message = $"Row {row.Key} has vertical alignment drift (top variance {topVariance}px, center variance {centerVariance}px) at scale {simulatedScale:0.##}.",
                        };
                    }
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindPanelSpecificIssues(Type panelType, Control analysisSurface, LayoutDiagnosticsOptions options)
        {
            if (panelType == typeof(WileyWidget.WinForms.Controls.Panels.BudgetOverviewPanel))
            {
                foreach (var issue in FindBudgetOverviewSpecificIssues(analysisSurface, options.SimulatedScale))
                {
                    yield return issue;
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindBudgetOverviewSpecificIssues(Control analysisSurface, float simulatedScale)
        {
            var flows = analysisSurface.Controls.OfType<FlowLayoutPanel>().ToList();
            foreach (var flow in flows)
            {
                var cards = flow.Controls.Cast<Control>().Where(IsVisibleForLayout).Where(LooksLikeSummaryCard).ToList();
                if (cards.Count < 4)
                {
                    continue;
                }

                var usedWidth = cards.Sum(card => card.Width + card.Margin.Horizontal);
                var unusedWidth = Math.Max(0, flow.ClientSize.Width - usedWidth);
                if (unusedWidth >= 220)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = "info",
                        Kind = "summary-row-balance",
                        ControlName = GetControlIdentifier(flow),
                        Message = $"KPI strip leaves about {unusedWidth}px unused at scale {simulatedScale:0.##}; the cards read as undersized for the available width.",
                    };
                }
            }

            foreach (var table in analysisSurface.Controls.OfType<TableLayoutPanel>())
            {
                var visibleControls = table.Controls.Cast<Control>().Where(IsVisibleForLayout).ToList();
                if (visibleControls.Count < 3)
                {
                    continue;
                }

                var contentWidth = visibleControls.Max(control => control.Right) - visibleControls.Min(control => control.Left);
                if (contentWidth > table.ClientSize.Width - 12)
                {
                    yield return new PanelLayoutIssue
                    {
                        Severity = "info",
                        Kind = "toolbar-fit",
                        ControlName = GetControlIdentifier(table),
                        Message = $"Toolbar content consumes about {contentWidth}px of {table.ClientSize.Width}px at scale {simulatedScale:0.##}; labels and actions will feel cramped as DPI increases.",
                    };
                }
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindThemeIssues(Control root, string expectedThemeName)
        {
            if (string.IsNullOrWhiteSpace(expectedThemeName))
            {
                yield break;
            }

            var fieldControls = EnumerateControls(root)
                .Where(IsThemeAuditableField)
                .Where(IsVisibleForLayout)
                .Where(control => control.Enabled)
                .Where(control => !IsWithinOverlayLikeContainer(control))
                .Where(control => !HasFieldAncestor(control.Parent))
                .ToList();

            var explicitFieldThemeNames = fieldControls
                .Where(control => HasWritableStringProperty(control, "ThemeName"))
                .Select(control => GetStringProperty(control, "ThemeName"))
                .Where(themeName => !string.IsNullOrWhiteSpace(themeName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var control in EnumerateControls(root))
            {
                var actualThemeName = GetStringProperty(control, "ThemeName");
                if (string.IsNullOrWhiteSpace(actualThemeName))
                {
                    continue;
                }

                if (string.Equals(actualThemeName, expectedThemeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasAncestorWithSameThemeMismatch(control.Parent, actualThemeName, expectedThemeName))
                {
                    continue;
                }

                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "theme-mismatch",
                    ControlName = GetControlIdentifier(control),
                    Message = $"Control theme '{actualThemeName}' does not match expected theme '{expectedThemeName}'.",
                };
            }

            if (explicitFieldThemeNames.Count > 0)
            {
                var peerThemeName = explicitFieldThemeNames[0];
                foreach (var control in fieldControls)
                {
                    if (!HasWritableStringProperty(control, "ThemeName"))
                    {
                        continue;
                    }

                    var actualThemeName = GetStringProperty(control, "ThemeName");
                    if (!string.IsNullOrWhiteSpace(actualThemeName))
                    {
                        continue;
                    }

                    yield return new PanelLayoutIssue
                    {
                        Severity = "info",
                        Kind = "field-theme-unset",
                        ControlName = GetControlIdentifier(control),
                        Message = $"Field exposes ThemeName but it is unset while peer fields in this panel use '{peerThemeName}'.",
                    };
                }
            }

            foreach (var issue in FindFieldThemeConsistencyIssues(fieldControls))
            {
                yield return issue;
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindFieldThemeConsistencyIssues(IReadOnlyList<Control> fieldControls)
        {
            foreach (var category in fieldControls.GroupBy(GetFieldThemeCategory))
            {
                if (category.Count() < 3)
                {
                    continue;
                }

                var snapshots = category
                    .Select(control => new FieldThemeSnapshot(
                        control,
                        GetFieldThemeCategory(control),
                        GetStringProperty(control, "ThemeName"),
                        QuantizeColor(control.BackColor),
                        QuantizeColor(control.ForeColor),
                        BuildFieldThemeSignature(control)))
                    .ToList();

                var styleGroups = snapshots
                    .GroupBy(snapshot => snapshot.Signature)
                    .OrderByDescending(group => group.Count())
                    .ToList();

                if (styleGroups.Count <= 1)
                {
                    continue;
                }

                var primary = styleGroups[0].First();
                var secondary = styleGroups[1].First();
                var severity = HasStrongFieldThemeSplit(primary, secondary) ? "warning" : "info";
                var examples = string.Join(
                    "; ",
                    styleGroups.Take(3).Select(group => DescribeFieldThemeGroup(group.ToList())));

                yield return new PanelLayoutIssue
                {
                    Severity = severity,
                    Kind = "field-theme-inconsistency",
                    ControlName = GetControlIdentifier(primary.Control.Parent ?? primary.Control),
                    Message = $"{DescribeFieldThemeCategory(category.Key)} render with {styleGroups.Count} distinct theme styles instead of one consistent field surface. {examples}.",
                };
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindStructuralIssues(Control panel, Control analysisSurface, int visibleControlCount, int visibleLeafControlCount, long topLevelArea)
        {
            if (!ReferenceEquals(panel, analysisSurface))
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "info",
                    Kind = "analysis-surface",
                    ControlName = GetControlIdentifier(analysisSurface),
                    Message = "Metrics are measured against the panel content host rather than the outer shell.",
                };
            }

            if (visibleControlCount == 0 || topLevelArea == 0)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "inconclusive",
                    ControlName = GetControlIdentifier(analysisSurface),
                    Message = "No meaningful rendered content was detected on the analysis surface. This panel is excluded from aggregate layout metrics.",
                };
                yield break;
            }

            if (visibleLeafControlCount == 0)
            {
                yield return new PanelLayoutIssue
                {
                    Severity = "info",
                    Kind = "container-only",
                    ControlName = GetControlIdentifier(analysisSurface),
                    Message = "The analysis surface rendered containers only; internal content alignment may still require manual review.",
                };
            }
        }

        private static IEnumerable<PanelLayoutIssue> FindClippedControls(Control root)
        {
            foreach (var control in GetVisibleDescendants(root))
            {
                if (ShouldIgnoreUtilityControl(control))
                {
                    continue;
                }

                if (control.Parent == null || (control.Parent is ScrollableControl scrollableParent && scrollableParent.AutoScroll))
                {
                    continue;
                }

                if (control.Dock == DockStyle.Fill && control.Parent.GetType().Name.Contains("SplitPanel", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (control is Panel && control.Parent.GetType().Name.Contains("SplitPanel", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bounds = control.Bounds;
                var parentClient = GetEffectiveClientRectangle(control.Parent);
                if (bounds.Left >= parentClient.Left
                    && bounds.Top >= parentClient.Top
                    && bounds.Right <= parentClient.Right
                    && bounds.Bottom <= parentClient.Bottom)
                {
                    continue;
                }

                yield return new PanelLayoutIssue
                {
                    Severity = "warning",
                    Kind = "clipping",
                    ControlName = GetControlIdentifier(control),
                    Message = $"Bounds {bounds} exceed parent client {parentClient}.",
                };
            }
        }

        private static Rectangle GetEffectiveClientRectangle(Control parent)
        {
            var clientRectangle = parent.ClientRectangle;
            var displayRectangle = parent.DisplayRectangle;

            if (displayRectangle.Width > clientRectangle.Width || displayRectangle.Height > clientRectangle.Height)
            {
                return displayRectangle;
            }

            return clientRectangle;
        }

        private static IEnumerable<PanelLayoutIssue> FindOverlayIssues(Control root)
        {
            var rootArea = Math.Max(1L, (long)root.ClientSize.Width * root.ClientSize.Height);

            foreach (var control in GetVisibleDescendants(root))
            {
                if (!IsOverlayLike(control))
                {
                    continue;
                }

                if (HasOverlayAncestor(control.Parent))
                {
                    continue;
                }

                var overlayRect = Rectangle.Intersect(root.ClientRectangle, GetBoundsRelativeToRoot(control, root));
                if (overlayRect.IsEmpty)
                {
                    continue;
                }

                var coverage = 100d * ((long)overlayRect.Width * overlayRect.Height) / rootArea;
                if (coverage < 25d)
                {
                    continue;
                }

                var zOrder = control.Parent?.Controls.GetChildIndex(control) ?? -1;
                yield return new PanelLayoutIssue
                {
                    Severity = coverage >= 50d ? "warning" : "info",
                    Kind = "overlay",
                    ControlName = GetControlIdentifier(control),
                    Message = $"Overlay covers {Round(coverage)}% of the panel and has z-index {zOrder}.",
                };
            }
        }

        private static bool IsOverlayLike(Control control)
        {
            var identifier = GetControlIdentifier(control);
            return identifier.Contains("overlay", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("loader", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("loading", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("nodata", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWithinOverlayLikeContainer(Control control)
        {
            Control? current = control;
            while (current != null)
            {
                if (IsOverlayLike(current))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static bool HasOverlayAncestor(Control? control)
        {
            var current = control;
            while (current != null)
            {
                if (IsOverlayLike(current))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static string GetControlIdentifier(Control control)
        {
            var segments = new List<string>();
            Control? current = control;

            while (current != null)
            {
                segments.Add(string.IsNullOrWhiteSpace(current.Name) ? current.GetType().Name : current.Name);
                if (current.Parent == null || current.Parent is Form)
                {
                    break;
                }

                current = current.Parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            yield return root;
            foreach (var descendant in GetVisibleDescendants(root))
            {
                yield return descendant;
            }
        }

        private static bool ShouldInspectTextFit(Control control)
        {
            if (control is Label or ButtonBase or ComboBox or CheckBox or RadioButton or GroupBox)
            {
                return true;
            }

            var typeName = control.GetType().Name;
            return typeName.Contains("Button", StringComparison.Ordinal)
                || typeName.Contains("ComboBox", StringComparison.Ordinal)
                || typeName.Contains("Label", StringComparison.Ordinal);
        }

        private static bool IsSingleLineTextControl(Control control)
        {
            return control is not Label label || !label.AutoSize;
        }

        private static Size MeasureTextSingleLine(string text, Font font)
        {
            var lines = SplitLines(text);
            var widths = lines.Select(line => TextRenderer.MeasureText(line, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
            var lineHeight = TextRenderer.MeasureText("Ag", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
            return new Size(widths.DefaultIfEmpty(0).Max(), Math.Max(lineHeight, lineHeight * lines.Length));
        }

        private static Size MeasureTextForBounds(string text, Font font, int availableWidth)
        {
            var lines = SplitLines(text);
            var height = 0;
            var width = 0;

            foreach (var line in lines)
            {
                var measured = TextRenderer.MeasureText(
                    string.IsNullOrEmpty(line) ? " " : line,
                    font,
                    new Size(availableWidth, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                width = Math.Max(width, measured.Width);
                height += measured.Height;
            }

            return new Size(width, height);
        }

        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        }

        private static string GetDisplayedText(Control control)
        {
            var text = control.Text?.Replace("&", string.Empty, StringComparison.Ordinal) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }

            var selectedItem = GetObjectProperty(control, "SelectedItem");
            return selectedItem?.ToString()?.Trim() ?? string.Empty;
        }

        private static Rectangle GetTextBounds(Control control)
        {
            var padding = control.Padding;
            var left = padding.Left;
            var top = padding.Top;
            var width = Math.Max(0, control.ClientSize.Width - padding.Left - padding.Right);
            var height = Math.Max(0, control.ClientSize.Height - padding.Top - padding.Bottom);

            if (IsButtonLike(control) && GetObjectProperty(control, "Image") != null)
            {
                width = Math.Max(0, width - Math.Min(control.Height, 28));
            }

            if (IsComboLike(control))
            {
                width = Math.Max(0, width - 28);
            }

            return new Rectangle(left, top, width, height);
        }

        private static bool IsButtonLike(Control control)
        {
            return control is ButtonBase or SfButton || control.GetType().Name.Contains("Button", StringComparison.Ordinal);
        }

        private static bool IsComboLike(Control control)
        {
            return control is ComboBox || control.GetType().Name.Contains("ComboBox", StringComparison.Ordinal);
        }

        private static bool LooksLikeSummaryCard(Control control)
        {
            return control is Panel panel
                && control is not FlowLayoutPanel
                && control is not TableLayoutPanel
                && (!string.IsNullOrWhiteSpace(panel.AccessibleName) && panel.AccessibleName.Contains("summary card", StringComparison.OrdinalIgnoreCase)
                    || panel.Controls.OfType<Label>().Count() >= 2);
        }

        private static bool ShouldIgnoreUtilityControl(Control control)
        {
            var typeName = control.GetType().Name;
            var identifier = GetControlIdentifier(control);
            return typeName.Contains("ClearButton", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("DropDownButton", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("ScrollBar", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("UpDown", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("TextBoxExt", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("ClearButton", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("DropDownButton", StringComparison.OrdinalIgnoreCase)
                || string.Equals(control.Name, "PART_ClearButton", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldIgnoreForContentArea(Control control)
        {
            var typeName = control.GetType().Name;
            return ShouldIgnoreUtilityControl(control)
                || control is StatusStrip or MenuStrip
                || typeName.Contains("ScrollBar", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Splitter", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Separator", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeDenseFinanceColumn(string headerText, string mappingName)
        {
            var combined = string.Concat(headerText, " ", mappingName).ToLowerInvariant();
            return FinanceDenseColumnTerms.Any(term => combined.Contains(term, StringComparison.Ordinal));
        }

        private static bool HasVisibleHorizontalScrollbar(Control control)
        {
            return GetVisibleDescendants(control).Any(child => child is HScrollBar
                || (child.GetType().Name.Contains("ScrollBar", StringComparison.OrdinalIgnoreCase)
                    && child.Width > child.Height * 2));
        }

        private static bool ShouldTreatAsTransientOverlayText(Control control, string text)
        {
            if (!IsWithinOverlayLikeContainer(control))
            {
                return false;
            }

            var normalized = text.Trim().ToLowerInvariant();
            return normalized.Contains("loading", StringComparison.Ordinal)
                || normalized.Contains("checking", StringComparison.Ordinal)
                || normalized.Contains("connecting", StringComparison.Ordinal)
                || normalized.Contains("ready", StringComparison.Ordinal)
                || normalized.Contains("initializ", StringComparison.Ordinal)
                || normalized.Contains("fetching", StringComparison.Ordinal)
                || normalized.Contains("refreshing", StringComparison.Ordinal)
                || normalized.Contains("sync", StringComparison.Ordinal);
        }

        private static bool HasAncestorWithSameThemeMismatch(Control? control, string actualThemeName, string expectedThemeName)
        {
            var current = control;
            while (current != null)
            {
                var parentThemeName = GetStringProperty(current, "ThemeName");
                if (!string.IsNullOrWhiteSpace(parentThemeName)
                    && string.Equals(parentThemeName, actualThemeName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(parentThemeName, expectedThemeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static bool HasFieldAncestor(Control? control)
        {
            var current = control;
            while (current != null)
            {
                if (IsThemeAuditableField(current))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static bool IsThemeAuditableField(Control control)
        {
            if (ShouldIgnoreUtilityControl(control))
            {
                return false;
            }

            if (control is TextBoxBase or ComboBox or CheckBox or RadioButton or DateTimePicker or NumericUpDown)
            {
                return true;
            }

            var typeName = control.GetType().Name;
            return typeName.Contains("TextBox", StringComparison.Ordinal)
                || typeName.Contains("ComboBox", StringComparison.Ordinal)
                || typeName.Contains("Numeric", StringComparison.Ordinal)
                || typeName.Contains("CheckBox", StringComparison.Ordinal)
                || typeName.Contains("RadioButton", StringComparison.Ordinal)
                || typeName.Contains("DateTime", StringComparison.Ordinal)
                || typeName.Contains("DatePicker", StringComparison.Ordinal)
                || typeName.Contains("MaskedEdit", StringComparison.Ordinal);
        }

        private static string GetFieldThemeCategory(Control control)
        {
            var typeName = control.GetType().Name;
            return typeName.Contains("CheckBox", StringComparison.Ordinal)
                || typeName.Contains("RadioButton", StringComparison.Ordinal)
                || control is CheckBox
                || control is RadioButton
                ? "toggle-field"
                : "input-field";
        }

        private static string DescribeFieldThemeCategory(string category)
        {
            return string.Equals(category, "toggle-field", StringComparison.Ordinal)
                ? "Toggle fields"
                : "Input fields";
        }

        private static string BuildFieldThemeSignature(Control control)
        {
            var themeName = GetStringProperty(control, "ThemeName");
            var backColor = QuantizeColor(control.BackColor);
            var foreColor = QuantizeColor(control.ForeColor);
            return string.Join(
                "|",
                GetFieldThemeCategory(control),
                string.IsNullOrWhiteSpace(themeName) ? "<unset>" : themeName.Trim(),
                FormatColor(backColor),
                FormatColor(foreColor));
        }

        private static string DescribeFieldThemeGroup(IReadOnlyList<FieldThemeSnapshot> snapshots)
        {
            var sampleControlNames = string.Join(
                ", ",
                snapshots.Take(3).Select(snapshot => GetControlIdentifier(snapshot.Control).Split('/').Last()));
            var representative = snapshots[0];
            var themeName = string.IsNullOrWhiteSpace(representative.ThemeName) ? "<unset>" : representative.ThemeName;
            return $"{snapshots.Count} control(s) [{sampleControlNames}] theme '{themeName}' bg {FormatColor(representative.BackColor)} fg {FormatColor(representative.ForeColor)}";
        }

        private static bool HasStrongFieldThemeSplit(FieldThemeSnapshot primary, FieldThemeSnapshot secondary)
        {
            if (!string.Equals(primary.ThemeName, secondary.ThemeName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var backColorDistance = CalculateColorDistance(primary.BackColor, secondary.BackColor);
            var foreColorDistance = CalculateColorDistance(primary.ForeColor, secondary.ForeColor);
            return backColorDistance >= 48 || foreColorDistance >= 48;
        }

        private static int CalculateColorDistance(Color left, Color right)
        {
            return Math.Abs(left.R - right.R)
                + Math.Abs(left.G - right.G)
                + Math.Abs(left.B - right.B)
                + (Math.Abs(left.A - right.A) / 2);
        }

        private static Color QuantizeColor(Color color)
        {
            if (color.IsEmpty)
            {
                return Color.Empty;
            }

            static int QuantizeComponent(byte value)
            {
                return (int)Math.Round(value / 24d) * 24;
            }

            return Color.FromArgb(
                color.A,
                Math.Clamp(QuantizeComponent(color.R), 0, 255),
                Math.Clamp(QuantizeComponent(color.G), 0, 255),
                Math.Clamp(QuantizeComponent(color.B), 0, 255));
        }

        private static string FormatColor(Color color)
        {
            return color.IsEmpty
                ? "<empty>"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static PolishReview BuildPolishReview(Type panelType, Control analysisSurface, IReadOnlyList<PanelLayoutIssue> issues, bool meaningfulContentDetected, float simulatedScale)
        {
            var findings = new List<PanelPolishFinding>();

            foreach (var issue in issues)
            {
                var finding = TryCreatePolishFinding(issue);

                if (finding != null)
                {
                    findings.Add(finding);
                }
            }

            var score = CalculatePolishScore(issues, meaningfulContentDetected);
            var displayName = ResolveDisplayName(panelType);
            var summary = BuildPolishSummary(displayName, score, findings, meaningfulContentDetected, simulatedScale, analysisSurface.ClientSize);
            return new PolishReview(score, summary, findings);
        }

        private static VisualReview BuildVisualReview(Type panelType, Control analysisSurface, IReadOnlyList<PanelLayoutIssue> issues, bool meaningfulContentDetected, float simulatedScale)
        {
            var visualIssues = issues.Where(IsVisualIssue).ToList();
            var findings = new List<PanelPolishFinding>(visualIssues.Count);

            foreach (var issue in visualIssues)
            {
                var finding = TryCreatePolishFinding(issue);
                if (finding != null)
                {
                    findings.Add(finding);
                }
            }

            var displayName = ResolveDisplayName(panelType);
            var score = CalculateVisualScore(visualIssues, meaningfulContentDetected);
            var summary = BuildVisualSummary(displayName, score, findings, meaningfulContentDetected, simulatedScale, analysisSurface.ClientSize);
            return new VisualReview(score, summary, findings);
        }

        private static PanelPolishFinding? TryCreatePolishFinding(PanelLayoutIssue issue)
        {
            return issue.Kind switch
            {
                "panel-header" => CreatePolishFinding(
                    issue,
                    "First Impression & Orientation",
                    "The panel lacks a visible header region.",
                    "Users land on content without a clear title or top-of-surface orientation cue, which makes the panel feel unfinished.",
                    "Add a visible PanelHeader or equivalent top title band so the panel purpose and context are obvious immediately."),
                "autoscale-mode" => CreatePolishFinding(
                    issue,
                    "Resize & DPI",
                    "The root panel is not configured for DPI-aware autoscaling.",
                    "A panel can look acceptable on one monitor and then break or crowd at 125% or 150% scaling.",
                    "Set AutoScaleMode = AutoScaleMode.Dpi on the root panel so the layout follows Windows scaling rules."),
                "root-dock" => CreatePolishFinding(
                    issue,
                    "Responsive Layout",
                    "The root panel is not filling its host surface.",
                    "Panels that do not fill their shell leave dead space and behave unpredictably when the working area changes.",
                    "Use DockStyle.Fill on the root panel unless there is a documented reason to behave as an embedded fragment."),
                "minimum-size" => CreatePolishFinding(
                    issue,
                    "Responsive Layout",
                    "The panel minimum size baseline is weak or missing.",
                    "Complex data-entry or analytics panels become fragile when users shrink the surface or move between displays.",
                    "Declare a meaningful MinimumSize so the working layout stays viable under resize and DPI pressure."),
                "panel-header-clipping" => CreatePolishFinding(
                    issue,
                    "Clipping & Truncation",
                    "The panel header action strip is clipping at the right edge.",
                    "Top-level actions such as refresh, help, pin, or close can disappear or feel broken even though the rest of the panel loads.",
                    "Increase the header action strip width budget or reduce competing title/header chrome so the full action set remains visible at the target DPI."),
                "accessible-name" => CreatePolishFinding(
                    issue,
                    "Accessibility Baseline",
                    "Interactive controls are missing accessible names.",
                    "Keyboard and screen-reader users lose context and the panel falls below a professional accessibility baseline.",
                    "Assign AccessibleName to icon-only buttons, inputs, grids, charts, and other interactive surfaces that do not expose a reliable built-in label."),
                "text-truncation" => CreatePolishFinding(
                    issue,
                    "Clipping & Truncation",
                    "Readable text is being squeezed or cut off.",
                    "A finance user has to stop and infer what the control or message was supposed to say instead of scanning the screen quickly.",
                    "Increase the control width or height, or for labels set a wider container and enable wrapping. For grid headers, increase GridColumn.MinimumWidth or HeaderRowHeight."),
                "overlay-placeholder-text" => CreatePolishFinding(
                    issue,
                    "Empty State / Overlay",
                    "A transient loading or status message is using ellipsis text during diagnostics.",
                    "This usually reflects an intermediate state rather than a true polish defect, but it can still obscure how the panel feels before data arrives.",
                    "Keep transient overlay copy readable and centered, but avoid treating loading ellipses as equivalent to clipping in the working state."),
                "text-clipping" => CreatePolishFinding(
                    issue,
                    "Clipping & Truncation",
                    "Text height is too tight for the current DPI-scaled font.",
                    "The screen feels slightly broken at higher DPI and users lose confidence in the numbers shown.",
                    "Increase the hosting control height, or reduce competing padding so the text can render cleanly at 125% and 150% scaling."),
                "grid-header-truncation" => CreatePolishFinding(
                    issue,
                    "Grid Header Polish",
                    "A grid header is likely to truncate under the current column sizing.",
                    "Users spend extra time deciphering abbreviated headers and are more likely to misread the wrong column.",
                    "Raise the column Width or MinimumWidth, or switch the affected column away from aggressive Fill sizing. If needed, increase SfDataGrid.HeaderRowHeight."),
                "grid-header-height" => CreatePolishFinding(
                    issue,
                    "Grid Header Polish",
                    "A grid header row is too short for the current header font and wrapping pressure.",
                    "Users get a cramped first impression and multi-word column headers feel unstable as DPI increases.",
                    "Increase SfDataGrid.HeaderRowHeight or reduce competing header padding so the header can breathe at 125% and 150% scaling."),
                "grid-column-width" => CreatePolishFinding(
                    issue,
                    "Grid Density & Legibility",
                    "A finance-oriented grid column is narrower than a comfortable scanning width.",
                    "Monetary values, account identifiers, and dates become harder to compare quickly, which raises the odds of a bad operational read.",
                    "Raise Width or MinimumWidth for the affected column and reserve Fill sizing for low-density text fields."),
                "grid-row-height" => CreatePolishFinding(
                    issue,
                    "Grid Density & Legibility",
                    "Grid rows are shorter than the scaled font comfortably supports.",
                    "Rows look cramped and daily scanning fatigue goes up, especially on number-heavy screens.",
                    "Increase RowHeight so it lands at or above about 1.4x the effective font height for the diagnostic scale."),
                "grid-horizontal-scroll" => CreatePolishFinding(
                    issue,
                    "Grid Density & Legibility",
                    "The main grid already requires horizontal scrolling at the simulated scale.",
                    "Users lose vertical working space and have to pan sideways to verify totals or compare adjacent fields.",
                    "Widen the hosting surface, simplify low-value columns, or assign minimum widths more deliberately so the main workflow stays visible without panning."),
                "chart-surface-sizing" => CreatePolishFinding(
                    issue,
                    "Chart Density & Legibility",
                    "A chart surface is too small for reliable finance-style reading.",
                    "Axis labels, legends, and currency series compete for too little space, which makes trend reading feel cramped.",
                    "Increase the chart host size or reduce nearby gutters so the plotted area can support titles, legends, and scaled labels cleanly."),
                "chart-axis-padding" => CreatePolishFinding(
                    issue,
                    "Chart Density & Legibility",
                    "A chart is using tight internal spacing while axis labeling pressure is high.",
                    "Rotated labels and titled axes are more likely to crowd the plot area or clip near the edges as DPI increases.",
                    "Increase chart element spacing or hosting size when rotated labels or finance-oriented axis titles are active."),
                "chart-axis-label-fit" => CreatePolishFinding(
                    issue,
                    "Chart Density & Legibility",
                    "Rotated chart labels do not have much vertical breathing room.",
                    "Bottom-axis labels can dominate the plot or clip first when the user runs at 125% or 150% scaling.",
                    "Give the chart more vertical space, reduce competing chrome, or simplify the label cadence so rotated labels remain readable."),
                "chart-legend-crowding" => CreatePolishFinding(
                    issue,
                    "Chart Density & Legibility",
                    "The chart legend is crowding the visual surface.",
                    "The legend starts competing with the plotted data, which makes the panel feel cramped and less presentation-ready.",
                    "Reduce legend entries, move the legend to a roomier edge, or increase chart width so the legend and plot area can breathe."),
                "control-sizing" => CreatePolishFinding(
                    issue,
                    "Control Sizing & Usability",
                    "An interactive control is smaller than it should be for repeated daily use.",
                    "Small controls slow down clicking, reduce readability, and make the panel feel cramped on a 1080p display at 125% scaling.",
                    "Increase MinimumSize or explicit Size. For buttons and selectors in this repo, align to LayoutTokens.StandardControlHeightLarge and a width that comfortably fits the caption."),
                "summary-card-sizing" => CreatePolishFinding(
                    issue,
                    "Metric Card Readability",
                    "A KPI card is too small for finance-grade at-a-glance scanning.",
                    "Large balances and percentages feel compressed, which weakens trust in the dashboard even when the values are correct.",
                    "Increase the card width and height, or reduce surrounding gutters so the summary surface can carry larger numeric text comfortably."),
                "alignment-row" => CreatePolishFinding(
                    issue,
                    "Alignment & Visual Hierarchy",
                    "Controls in the same row are not visually centered against each other.",
                    "The toolbar or filter area looks improvised instead of deliberate, which slows down scanning even when everything technically works.",
                    "Normalize control heights, disable stray AutoSize behavior on labels in that row, and center-align contents within the TableLayoutPanel."),
                "alignment-spacing" => CreatePolishFinding(
                    issue,
                    "Alignment & Visual Hierarchy",
                    "Spacing inside a shared row is visually inconsistent.",
                    "The row reads as hand-placed instead of systematized, which makes dense finance screens feel lower quality.",
                    "Normalize sibling margins and use consistent gap tokens so the row lands on an intentional rhythm."),
                "alignment-card-balance" => CreatePolishFinding(
                    issue,
                    "Alignment & Visual Hierarchy",
                    "Summary cards in the same strip do not share a balanced width.",
                    "Users interpret the KPI row as unstable and have to work harder to compare values side by side.",
                    "Move the cards to fixed columns or normalize their widths so the strip reads as one deliberate dashboard band."),
                "summary-row-balance" => CreatePolishFinding(
                    issue,
                    "Alignment & Visual Hierarchy",
                    "The KPI row is not using the available width well.",
                    "The cards read as undersized and the top of the panel feels visually weak compared to the amount of open space around it.",
                    "Replace the FlowLayoutPanel KPI strip with a five-column TableLayoutPanel or increase each card width and reduce the leftover whitespace."),
                "toolbar-fit" => CreatePolishFinding(
                    issue,
                    "Control Sizing & Usability",
                    "The top toolbar is already width-constrained before higher DPI pressure is applied.",
                    "This is the kind of row that starts to clip or misalign first when the user changes monitor scale.",
                    "Use explicit fixed widths for the key actions, add a stretch column, and keep label and selector heights consistent with the action buttons."),
                "theme-mismatch" => CreatePolishFinding(
                    issue,
                    "Theme Consistency",
                    "A control is not following the active Syncfusion theme.",
                    "A single off-theme control makes the panel look less trustworthy and less professionally finished.",
                    "Ensure the control ThemeName matches the active theme or rely on SfSkinManager theme cascade from the parent surface."),
                "field-theme-unset" => CreatePolishFinding(
                    issue,
                    "Theme Consistency",
                    "A field control is not carrying the same explicit theme contract as its peers.",
                    "Field surfaces can drift away from the rest of the panel after dynamic creation, rebinding, or runtime theme switches.",
                    "When peer fields use explicit ThemeName values, apply the same active theme to this field or re-run the recursive theme pass after control creation."),
                "field-theme-inconsistency" => CreatePolishFinding(
                    issue,
                    "Theme Consistency",
                    "Field controls inside the same panel are rendering with conflicting visual themes.",
                    "Mixed dark and light field treatments make a form feel broken even when the data and commands still work.",
                    "Reapply the active Syncfusion theme recursively to the panel and verify text boxes, numeric inputs, selectors, and toggles share one coherent field palette."),
                "overlay" => CreatePolishFinding(
                    issue,
                    "Empty State / Overlay",
                    "The panel is dominated by an overlay state during diagnostics.",
                    "This is useful to know because empty-state layout can mask the real working layout that users see most often.",
                    "Keep the overlay content readable and centered, but also rerun diagnostics with representative data when evaluating day-to-day polish."),
                "empty-graphics-surface" => CreatePolishFinding(
                    issue,
                    "Rendered Content",
                    "A large graphics or preview surface rendered mostly as background pixels.",
                    "Users see a blank report, chart, PDF preview, or embedded web surface even though the surrounding panel chrome appears ready.",
                    "Verify the surface is initialized after the final layout pass and that the hosted control is painting real content before diagnostics capture runs."),
                "inconclusive" => CreatePolishFinding(
                    issue,
                    "Diagnostics Coverage",
                    "The panel did not render enough real content for a trustworthy polish review.",
                    "A shell-only result can hide the actual layout issues that matter once data loads.",
                    "Load representative data or activate the hosted content path before using this panel in aggregate polish decisions."),
                _ => null,
            };
        }

        private static PanelPolishFinding CreatePolishFinding(PanelLayoutIssue issue, string category, string problem, string userImpact, string recommendation)
        {
            return new PanelPolishFinding
            {
                Severity = issue.Severity,
                Category = category,
                Problem = problem,
                UserImpact = userImpact,
                Recommendation = recommendation,
                Evidence = issue.Message,
            };
        }

        private static int CalculatePolishScore(IReadOnlyList<PanelLayoutIssue> issues, bool meaningfulContentDetected)
        {
            var score = 100;

            foreach (var issue in issues)
            {
                score -= GetIssueScorePenalty(issue);
            }

            if (!meaningfulContentDetected)
            {
                score -= 10;
            }

            return Math.Max(0, score);
        }

        private static int CalculateVisualScore(IReadOnlyList<PanelLayoutIssue> visualIssues, bool meaningfulContentDetected)
        {
            if (!meaningfulContentDetected)
            {
                return 0;
            }

            var score = 100;
            foreach (var issue in visualIssues)
            {
                score -= GetIssueScorePenalty(issue);
            }

            return Math.Max(0, score);
        }

        private static int GetIssueScorePenalty(PanelLayoutIssue issue)
        {
            return issue.Kind switch
            {
                "panel-header" => 6,
                "autoscale-mode" => 4,
                "root-dock" => 3,
                "minimum-size" => issue.Severity == "warning" ? 4 : 2,
                "panel-header-clipping" => 8,
                "accessible-name" => 4,
                "text-truncation" => 8,
                "overlay-placeholder-text" => 1,
                "text-clipping" => 7,
                "grid-header-truncation" => 7,
                "grid-header-height" => 4,
                "grid-column-width" => issue.Severity == "warning" ? 6 : 3,
                "grid-row-height" => 5,
                "grid-horizontal-scroll" => 3,
                "chart-surface-sizing" => issue.Severity == "warning" ? 4 : 2,
                "chart-axis-padding" => issue.Severity == "warning" ? 4 : 2,
                "chart-axis-label-fit" => issue.Severity == "warning" ? 4 : 2,
                "chart-legend-crowding" => issue.Severity == "warning" ? 4 : 2,
                "clipping" => 6,
                "theme-mismatch" => 5,
                "field-theme-unset" => 2,
                "field-theme-inconsistency" => issue.Severity == "warning" ? 6 : 3,
                "control-sizing" => issue.Severity == "warning" ? 5 : 2,
                "summary-card-sizing" => issue.Severity == "warning" ? 4 : 2,
                "alignment-row" => 3,
                "alignment-spacing" => 2,
                "alignment-card-balance" => 3,
                "summary-row-balance" => 3,
                "toolbar-fit" => 3,
                "overlay" => 2,
                "empty-graphics-surface" => 7,
                "inconclusive" => 10,
                _ => issue.Severity == "warning" ? 3 : 1,
            };
        }

        private static string BuildPolishSummary(string displayName, int score, IReadOnlyList<PanelPolishFinding> findings, bool meaningfulContentDetected, float simulatedScale, Size clientSize)
        {
            if (!meaningfulContentDetected)
            {
                return $"{displayName} could not be scored reliably because the diagnostics run did not render a meaningful working surface at {simulatedScale:0.##} scale.";
            }

            var warningCount = findings.Count(finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase));
            var infoCount = findings.Count - warningCount;
            var grade = score switch
            {
                >= 90 => "mostly polished",
                >= 80 => "close, but still rough in a few places",
                >= 70 => "usable but visibly uneven",
                _ => "functionally there but not yet polished",
            };

            return $"{displayName} scores {score}/100 at {simulatedScale:0.##} scale on a {clientSize.Width}x{clientSize.Height} client surface. It is {grade}, with {warningCount} higher-priority and {infoCount} lower-priority polish findings.";
        }

        private static string BuildVisualSummary(string displayName, int score, IReadOnlyList<PanelPolishFinding> findings, bool meaningfulContentDetected, float simulatedScale, Size clientSize)
        {
            if (!meaningfulContentDetected)
            {
                return $"{displayName} did not render a meaningful working surface at {simulatedScale:0.##} scale, so its visual appearance could not be scored reliably.";
            }

            var warningCount = findings.Count(finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase));
            var infoCount = findings.Count - warningCount;
            var grade = score switch
            {
                >= 90 => "visually strong",
                >= 80 => "visually solid with a few rough edges",
                >= 70 => "visually workable but uneven",
                _ => "visually underdeveloped",
            };

            return $"{displayName} has a visual score of {score}/100 at {simulatedScale:0.##} scale on a {clientSize.Width}x{clientSize.Height} client surface. It is {grade}, with {warningCount} higher-priority and {infoCount} lower-priority appearance findings.";
        }

        private static bool IsVisualIssue(PanelLayoutIssue issue)
        {
            return issue.Kind switch
            {
                "panel-header" => true,
                "autoscale-mode" => true,
                "root-dock" => true,
                "minimum-size" => true,
                "panel-header-clipping" => true,
                "text-truncation" => true,
                "overlay-placeholder-text" => true,
                "text-clipping" => true,
                "grid-header-truncation" => true,
                "grid-header-height" => true,
                "grid-column-width" => true,
                "grid-row-height" => true,
                "grid-horizontal-scroll" => true,
                "chart-surface-sizing" => true,
                "chart-axis-padding" => true,
                "chart-axis-label-fit" => true,
                "chart-legend-crowding" => true,
                "clipping" => true,
                "theme-mismatch" => true,
                "field-theme-unset" => true,
                "field-theme-inconsistency" => true,
                "control-sizing" => true,
                "summary-card-sizing" => true,
                "alignment-row" => true,
                "alignment-spacing" => true,
                "alignment-card-balance" => true,
                "summary-row-balance" => true,
                "toolbar-fit" => true,
                "overlay" => true,
                "empty-graphics-surface" => true,
                _ => false,
            };
        }

        private static PanelChecklistSummary BuildChecklistReview(
            Type panelType,
            Control panel,
            Control analysisSurface,
            IReadOnlyList<PanelLayoutIssue> issues,
            bool meaningfulContentDetected,
            double topLevelWhitespacePercent,
            double leafWhitespacePercent,
            int leftWhitespace,
            int topWhitespace,
            int rightWhitespace,
            int bottomWhitespace)
        {
            var items = new List<PanelChecklistItem>();
            var displayName = ResolveDisplayName(panelType);

            var hasGrid = EnumerateControls(analysisSurface).OfType<SfDataGrid>().Any(IsVisibleForLayout);
            var hasChart = EnumerateControls(analysisSurface).OfType<ChartControl>().Any(IsVisibleForLayout);

            items.Add(CreateChecklistItem(
                "Purpose & First Impression",
                "panel-header",
                "Panel has a visible title/header region",
                HasIssue(issues, "panel-header") ? "fail" : "pass",
                HasIssueMessage(issues, "panel-header", "A visible PanelHeader or top title band is present."),
                "Use PanelHeader so the panel purpose and context are obvious within the first scan."));

            items.Add(CreateChecklistItem(
                "Structure & Resize",
                "autoscale-mode",
                "Root panel uses DPI-aware autoscaling",
                HasIssue(issues, "autoscale-mode") ? "fail" : "pass",
                HasIssueMessage(issues, "autoscale-mode", "AutoScaleMode is set to Dpi or inherited correctly."),
                "Keep the root panel on AutoScaleMode.Dpi so it survives scale changes cleanly."));

            items.Add(CreateChecklistItem(
                "Structure & Resize",
                "root-dock",
                "Root panel fills its host surface",
                HasIssue(issues, "root-dock") ? "fail" : "pass",
                HasIssueMessage(issues, "root-dock", "Root panel is docked to Fill."),
                "Dock the panel to Fill unless it is intentionally an embedded fragment."));

            items.Add(CreateChecklistItem(
                "Structure & Resize",
                "minimum-size",
                "Panel declares a viable minimum working size",
                HasIssue(issues, "minimum-size") ? "fail" : "pass",
                HasIssueMessage(issues, "minimum-size", $"MinimumSize baseline is {panel.MinimumSize.Width}x{panel.MinimumSize.Height}px."),
                "Use a minimum size that preserves working layout under resize and DPI pressure."));

            items.Add(CreateChecklistItem(
                "Purpose & First Impression",
                "meaningful-content",
                "Diagnostics rendered a meaningful working surface",
                meaningfulContentDetected ? "pass" : "fail",
                meaningfulContentDetected
                    ? $"The diagnostics run rendered content on {displayName} rather than a shell-only surface."
                    : "The diagnostics run did not render enough working content for a trustworthy visual audit.",
                "Prime representative data and active content paths before relying on the panel score."));

            items.Add(CreateChecklistItem(
                "Layout & Spacing",
                "content-balance",
                "Whitespace balance stays within a usable range",
                EvaluateWhitespaceStatus(topLevelWhitespacePercent, leftWhitespace, rightWhitespace, topWhitespace, bottomWhitespace, analysisSurface.ClientSize),
                $"Top-level whitespace {topLevelWhitespacePercent:0.#}% and leaf whitespace {leafWhitespacePercent:0.#}%; edges L{leftWhitespace}/T{topWhitespace}/R{rightWhitespace}/B{bottomWhitespace}.",
                "Extreme whitespace is a prompt to review grouping, docking, and content density against the panel checklist."));

            items.Add(CreateChecklistItem(
                "Typography & Legibility",
                "text-fit",
                "Readable text is not clipping or truncating",
                HasIssue(issues, "text-truncation", "text-clipping", "clipping", "grid-header-truncation", "grid-header-height") ? "fail" : "pass",
                HasIssueMessage(issues, new[] { "text-truncation", "text-clipping", "clipping", "grid-header-truncation", "grid-header-height" }, "No text-fit defects were detected in labels, controls, or grid headers."),
                "Widen, wrap, or resize the affected surfaces so text reads cleanly at the simulated scale."));

            items.Add(CreateChecklistItem(
                "Controls & Forms",
                "interactive-sizing",
                "Interactive controls meet a comfortable working size",
                HasIssue(issues, "control-sizing", "summary-card-sizing") ? "fail" : "pass",
                HasIssueMessage(issues, new[] { "control-sizing", "summary-card-sizing" }, "Buttons, selectors, and summary cards are sized comfortably for repeated use."),
                "Raise control heights and widths so daily finance workflows do not feel cramped."));

            items.Add(CreateChecklistItem(
                "Layout & Spacing",
                "alignment-rhythm",
                "Rows, gaps, and card bands feel aligned and deliberate",
                HasIssue(issues, "alignment-row", "alignment-spacing", "alignment-card-balance", "summary-row-balance", "toolbar-fit") ? "fail" : "pass",
                HasIssueMessage(issues, new[] { "alignment-row", "alignment-spacing", "alignment-card-balance", "summary-row-balance", "toolbar-fit" }, "No major row, spacing, or KPI balance issues were detected."),
                "Normalize margins, widths, and stretch columns so the surface reads as one deliberate system."));

            items.Add(CreateChecklistItem(
                "Theme & Contrast",
                "theme-consistency",
                "Visible controls follow the active application theme",
                HasIssue(issues, "theme-mismatch", "field-theme-inconsistency") ? "fail" : "pass",
                HasIssueMessage(issues, new[] { "theme-mismatch", "field-theme-inconsistency", "field-theme-unset" }, "No theme mismatch was detected among themed controls or field surfaces."),
                "Keep theme application centralized through SfSkinManager and the active ThemeName."));

            items.Add(CreateChecklistItem(
                "Accessibility Baseline",
                "accessible-name",
                "Interactive controls expose an accessible name baseline",
                HasIssue(issues, "accessible-name") ? "fail" : "pass",
                HasIssueMessage(issues, "accessible-name", "No missing AccessibleName baseline defects were detected for textless or high-value interactive controls."),
                "Assign AccessibleName to icon-only controls, input surfaces, grids, charts, and other interactive elements that do not present a reliable built-in label."));

            items.Add(CreateChecklistItem(
                "Data-Dense Surfaces",
                "grid-readability",
                "Grids remain readable without cramped headers, rows, or columns",
                hasGrid
                    ? (HasIssue(issues, "grid-header-truncation", "grid-header-height", "grid-column-width", "grid-row-height", "grid-horizontal-scroll") ? "fail" : "pass")
                    : "not-applicable",
                hasGrid
                    ? HasIssueMessage(issues, new[] { "grid-header-truncation", "grid-header-height", "grid-column-width", "grid-row-height", "grid-horizontal-scroll" }, "No grid density defects were detected at the simulated scale.")
                    : "No visible SfDataGrid surface was detected on this panel.",
                "Keep working columns, headers, and row heights comfortable at 125% and 150% scale."));

            items.Add(CreateChecklistItem(
                "Data Visualization",
                "chart-readability",
                "Charts preserve readable plot, label, and legend space",
                hasChart
                    ? (HasIssue(issues, "chart-surface-sizing", "chart-axis-padding", "chart-axis-label-fit", "chart-legend-crowding") ? "fail" : "pass")
                    : "not-applicable",
                hasChart
                    ? HasIssueMessage(issues, new[] { "chart-surface-sizing", "chart-axis-padding", "chart-axis-label-fit", "chart-legend-crowding" }, "No chart density defects were detected at the simulated scale.")
                    : "No visible chart surface was detected on this panel.",
                "Give charts enough room for titles, legends, and axis labels before adding more chrome."));

            items.Add(CreateChecklistItem(
                "Purpose & First Impression",
                "purpose-manual",
                "A human reviewer confirms the panel's purpose is obvious within three seconds",
                "manual-review",
                "Automation cannot reliably judge whether the panel's mission and focal point are immediately obvious to a first-time user.",
                "Review the panel against the canonical checklist sections for purpose, focal point, and first action clarity."));

            items.Add(CreateChecklistItem(
                "Typography & Copy",
                "copy-manual",
                "A human reviewer confirms titles, labels, and commands use concise Windows-style copy",
                "manual-review",
                "Automation can detect clipping, but it cannot fully judge tone, sentence case quality, or wording clarity.",
                "Review titles, labels, tooltips, and action verbs against the Microsoft-derived copy rules in the UI standard."));

            items.Add(CreateChecklistItem(
                "Controls & Forms",
                "form-labels-manual",
                "A human reviewer confirms form grouping and label placement match the form checklist",
                "manual-review",
                "Automation cannot reliably determine whether labels are semantically above-field, grouped well, or overwhelming for the workflow.",
                "Review label placement, group spacing, control choice, and validation flow against the form section of the standard."));

            items.Add(CreateChecklistItem(
                "Commanding & Navigation",
                "command-manual",
                "A human reviewer confirms primary commands, destructive actions, and navigation cues are intentional",
                "manual-review",
                "Automation cannot fully judge whether the right actions are primary, secondary, reversible, or buried too deeply.",
                "Review command prominence, confirmation strategy, undo paths, and navigation clarity manually."));

            items.Add(CreateChecklistItem(
                "Accessibility Baseline",
                "keyboard-manual",
                "A human reviewer confirms keyboard-only flow, focus travel, and shortcut discoverability",
                "manual-review",
                "Automation can check naming baselines, but it cannot fully validate tab order quality, F6 pane flow, or shortcut usefulness from the control tree alone.",
                "Run a keyboard-only review for tab order, focus visibility, major pane travel, and shortcut discoverability."));

            items.Add(CreateChecklistItem(
                "Feedback & Recovery",
                "state-manual",
                "A human reviewer confirms loading, empty, success, and error states feel intentional",
                "manual-review",
                "Automation can spot overlays, but it cannot fully judge whether the messaging and state transitions feel polished in real use.",
                "Review empty states, overlays, error copy, and success feedback using representative data and workflow transitions."));

            var automatedItems = items.Where(item => item.Status is "pass" or "fail").ToList();
            var passed = items.Count(item => item.Status == "pass");
            var failed = items.Count(item => item.Status == "fail");
            var manualReview = items.Count(item => item.Status == "manual-review");
            var notApplicable = items.Count(item => item.Status == "not-applicable");
            var score = automatedItems.Count == 0
                ? 100
                : (int)Math.Round(100d * automatedItems.Count(item => item.Status == "pass") / automatedItems.Count, MidpointRounding.AwayFromZero);

            var summary = $"{displayName} passes {passed} automated checklist items, fails {failed}, and still needs {manualReview} manual review calls from the canonical panel checklist.";

            return new PanelChecklistSummary
            {
                Version = PanelChecklistSummary.CurrentVersion,
                Score = score,
                Passed = passed,
                Failed = failed,
                ManualReview = manualReview,
                NotApplicable = notApplicable,
                Summary = summary,
                Items = items,
            };
        }

        private static PanelChecklistItem CreateChecklistItem(string section, string checkId, string title, string status, string evidence, string recommendation)
        {
            return new PanelChecklistItem
            {
                Section = section,
                CheckId = checkId,
                Title = title,
                Status = status,
                Evidence = evidence,
                Recommendation = recommendation,
            };
        }

        private static string EvaluateWhitespaceStatus(double topLevelWhitespacePercent, int leftWhitespace, int rightWhitespace, int topWhitespace, int bottomWhitespace, Size clientSize)
        {
            if (topLevelWhitespacePercent >= 85d)
            {
                return "fail";
            }

            var horizontalEdgeDrift = Math.Abs(leftWhitespace - rightWhitespace);
            var verticalEdgeDrift = Math.Abs(topWhitespace - bottomWhitespace);
            if (topLevelWhitespacePercent >= 70d
                || horizontalEdgeDrift > Math.Max(32, clientSize.Width / 4)
                || verticalEdgeDrift > Math.Max(40, clientSize.Height / 4))
            {
                return "manual-review";
            }

            return "pass";
        }

        private static bool HasIssue(IReadOnlyList<PanelLayoutIssue> issues, params string[] kinds)
        {
            return issues.Any(issue => kinds.Any(kind => string.Equals(issue.Kind, kind, StringComparison.OrdinalIgnoreCase)));
        }

        private static string HasIssueMessage(IReadOnlyList<PanelLayoutIssue> issues, string kind, string fallback)
        {
            return HasIssueMessage(issues, new[] { kind }, fallback);
        }

        private static string HasIssueMessage(IReadOnlyList<PanelLayoutIssue> issues, IEnumerable<string> kinds, string fallback)
        {
            var messages = issues
                .Where(issue => kinds.Any(kind => string.Equals(issue.Kind, kind, StringComparison.OrdinalIgnoreCase)))
                .Select(issue => issue.Message)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToList();

            return messages.Count == 0 ? fallback : string.Join(" ", messages);
        }

        private static bool HasVisiblePanelHeader(Control root)
        {
            return EnumerateControls(root).OfType<PanelHeader>().Any(IsVisibleForLayout);
        }

        private static IEnumerable<Control> FindInteractiveControlsMissingAccessibleNames(Control root)
        {
            foreach (var control in EnumerateControls(root))
            {
                if (!IsVisibleForLayout(control) || ShouldIgnoreUtilityControl(control) || !RequiresAccessibleNameBaseline(control))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(control.AccessibleName))
                {
                    continue;
                }

                if (ControlHasImplicitAccessibleText(control))
                {
                    continue;
                }

                yield return control;
            }
        }

        private static bool RequiresAccessibleNameBaseline(Control control)
        {
            if (IsButtonLike(control) || IsComboLike(control) || control is TextBoxBase or SfDataGrid or ChartControl)
            {
                return true;
            }

            var typeName = control.GetType().Name;
            return typeName.Contains("TextBox", StringComparison.Ordinal)
                || typeName.Contains("Grid", StringComparison.Ordinal)
                || typeName.Contains("ListView", StringComparison.Ordinal)
                || typeName.Contains("TreeView", StringComparison.Ordinal)
                || typeName.Contains("Calendar", StringComparison.Ordinal);
        }

        private static bool ControlHasImplicitAccessibleText(Control control)
        {
            if (IsButtonLike(control))
            {
                return !string.IsNullOrWhiteSpace(GetDisplayedText(control));
            }

            return false;
        }

        private sealed record VisualReview(int Score, string Summary, IReadOnlyList<PanelPolishFinding> Findings);

        private sealed record PolishReview(int Score, string Summary, IReadOnlyList<PanelPolishFinding> Findings);

        private static int GetGridColumnWidth(GridColumnBase column)
        {
            var actualWidth = GetDoubleProperty(column, "ActualWidth");
            if (actualWidth > 0)
            {
                return (int)Math.Round(actualWidth, MidpointRounding.AwayFromZero);
            }

            var width = GetDoubleProperty(column, "Width");
            if (width > 0)
            {
                return (int)Math.Round(width, MidpointRounding.AwayFromZero);
            }

            var minimumWidth = GetDoubleProperty(column, "MinimumWidth");
            return minimumWidth > 0
                ? (int)Math.Round(minimumWidth, MidpointRounding.AwayFromZero)
                : 0;
        }

        private static double GetDoubleProperty(object? instance, string propertyName)
        {
            var value = GetObjectProperty(instance, propertyName);
            return value switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                long longValue => longValue,
                _ => 0d,
            };
        }

        private static string GetStringProperty(object? instance, string propertyName)
        {
            var value = GetObjectProperty(instance, propertyName);
            return value?.ToString() ?? string.Empty;
        }

        private static bool GetBoolProperty(object? instance, string propertyName)
        {
            var value = GetObjectProperty(instance, propertyName);
            return value switch
            {
                bool boolValue => boolValue,
                _ => false,
            };
        }

        private static object? GetObjectProperty(object? instance, string propertyName)
        {
            if (instance == null)
            {
                return null;
            }

            try
            {
                return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private static bool HasWritableStringProperty(object? instance, string propertyName)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return property?.CanWrite == true && property.PropertyType == typeof(string);
            }
            catch
            {
                return false;
            }
        }

        private static IReadOnlyList<LayoutTokenRecommendation> BuildRecommendations(IReadOnlyList<TokenFootprintSummary> aggregateFootprint)
        {
            var recommendations = new List<LayoutTokenRecommendation>();

            var panelPadding = aggregateFootprint.FirstOrDefault(token => token.PatternName == "PanelPadding");
            if (panelPadding != null && panelPadding.ApproxWhitespaceArea >= 500_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = "LayoutTokens.PanelPadding",
                    CurrentValue = LayoutTokens.PanelPadding.ToString(),
                    RecommendedValue = "10",
                    Reason = "Outer shell padding is contributing a large amount of empty edge area across panels.",
                    Evidence = $"Approx whitespace footprint: {panelPadding.ApproxWhitespaceArea:N0} px across {panelPadding.MatchCount} matches.",
                });
            }

            var uniformPadding6 = aggregateFootprint.FirstOrDefault(token => token.PatternName == "UniformPadding6");
            if (uniformPadding6 != null && uniformPadding6.ApproxWhitespaceArea >= 400_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = uniformPadding6.CandidateTokens,
                    CurrentValue = LayoutTokens.ContentInnerPadding.All.ToString(),
                    RecommendedValue = "5",
                    Reason = "Dense panel shells are leaving measurable internal gutters after the 125% layout pass.",
                    Evidence = $"Approx whitespace footprint: {uniformPadding6.ApproxWhitespaceArea:N0} px across {uniformPadding6.MatchCount} matches.",
                });
            }

            var uniformPadding8 = aggregateFootprint.FirstOrDefault(token => token.PatternName == "UniformPadding8");
            if (uniformPadding8 != null && uniformPadding8.ApproxWhitespaceArea >= 400_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = uniformPadding8.CandidateTokens,
                    CurrentValue = LayoutTokens.PanelPaddingCompact.All.ToString(),
                    RecommendedValue = "6",
                    Reason = "Dense panel shells are leaving measurable internal gutters after the 125% layout pass.",
                    Evidence = $"Approx whitespace footprint: {uniformPadding8.ApproxWhitespaceArea:N0} px across {uniformPadding8.MatchCount} matches.",
                });
            }

            var sectionPadding = aggregateFootprint.FirstOrDefault(token => token.PatternName == "SectionPanelPadding");
            if (sectionPadding != null && sectionPadding.ApproxWhitespaceArea >= 350_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = "LayoutTokens.SectionPanelPadding",
                    CurrentValue = "12,8,12,8",
                    RecommendedValue = "10,6,10,6",
                    Reason = "Card and section containers are reserving more horizontal and vertical inset than the content requires.",
                    Evidence = $"Approx whitespace footprint: {sectionPadding.ApproxWhitespaceArea:N0} px across {sectionPadding.MatchCount} matches.",
                });
            }

            var panelPaddingSpacious = aggregateFootprint.FirstOrDefault(token => token.PatternName == "PanelPaddingSpacious");
            if (panelPaddingSpacious != null && panelPaddingSpacious.ApproxWhitespaceArea >= 300_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = "LayoutTokens.PanelPaddingSpacious",
                    CurrentValue = "15",
                    RecommendedValue = "12",
                    Reason = "Large chart and grid host shells are reserving too much dead air around the primary content surface.",
                    Evidence = $"Approx whitespace footprint: {panelPaddingSpacious.ApproxWhitespaceArea:N0} px across {panelPaddingSpacious.MatchCount} matches.",
                });
            }

            var halfContentMargin = aggregateFootprint.FirstOrDefault(token => token.PatternName == "HalfContentMargin");
            if (halfContentMargin != null && halfContentMargin.ApproxWhitespaceArea >= 200_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = "LayoutTokens.ContentMargin / 2",
                    CurrentValue = "8",
                    RecommendedValue = "6",
                    Reason = "Repeated mid-row gutters are still consuming measurable area across dense panel content blocks.",
                    Evidence = $"Approx whitespace footprint: {halfContentMargin.ApproxWhitespaceArea:N0} px across {halfContentMargin.MatchCount} matches.",
                });
            }

            var contentMargin = aggregateFootprint.FirstOrDefault(token => token.PatternName == "ContentMargin");
            if (contentMargin != null && contentMargin.ApproxWhitespaceArea >= 600_000)
            {
                recommendations.Add(new LayoutTokenRecommendation
                {
                    TokenName = "LayoutTokens.ContentMargin",
                    CurrentValue = LayoutTokens.ContentMargin.ToString(),
                    RecommendedValue = "12",
                    Reason = "Inter-card and filter-row gaps are the largest recurring source of avoidable whitespace.",
                    Evidence = $"Approx whitespace footprint: {contentMargin.ApproxWhitespaceArea:N0} px across {contentMargin.MatchCount} matches.",
                });
            }

            return recommendations;
        }

        private static void SeedProgramServices(IServiceProvider provider)
        {
            var field = typeof(Program).GetField("_services", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, provider);
        }

        private static void InitializeSyncfusionRuntime(IServiceProvider provider, string themeName)
        {
            try
            {
                var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IConfiguration>(provider);
                InvokeProgramMethod("RegisterSyncfusionLicense", configuration);
            }
            catch
            {
                // Best-effort only. Diagnostics should still run without license initialization.
            }

            try
            {
                InvokeProgramMethod("InitializeTheme", provider);
            }
            catch
            {
                ThemeColors.EnsureThemeAssemblyLoadedForTheme(themeName);
                SfSkinManager.ApplicationVisualTheme = themeName;
            }
        }

        private static void InvokeProgramMethod(string methodName, object? argument)
        {
            var method = typeof(Program).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            method?.Invoke(null, new[] { argument });
        }

        private static void PerformLayoutRecursive(Control control)
        {
            control.PerformLayout();
            foreach (Control child in control.Controls)
            {
                PerformLayoutRecursive(child);
            }
        }

        private static T RunOnSta<T>(Func<T> action)
        {
            T? result = default;
            ExceptionDispatchInfo? capturedException = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                }
            })
            {
                IsBackground = true,
                Name = "LayoutDiagnosticsRunner",
            };

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            capturedException?.Throw();
            return result!;
        }

        private static double Round(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private sealed class MutableTokenFootprint
        {
            public MutableTokenFootprint(string patternName, string candidateTokens)
            {
                PatternName = patternName;
                CandidateTokens = candidateTokens;
            }

            public string PatternName { get; }

            public string CandidateTokens { get; }

            public int MatchCount { get; set; }

            public long ApproxWhitespaceArea { get; set; }
        }

        private sealed class TokenPattern
        {
            private const int PaddingTolerance = 1;

            private TokenPattern(string patternName, string candidateTokens, Padding logicalPadding, bool exactOnly)
            {
                PatternName = patternName;
                CandidateTokens = candidateTokens;
                LogicalPadding = logicalPadding;
                ScaledPadding = LayoutTokens.GetScaled(logicalPadding);
                ExactOnly = exactOnly;
            }

            public string PatternName { get; }

            public string CandidateTokens { get; }

            public Padding LogicalPadding { get; }

            public Padding ScaledPadding { get; }

            public bool ExactOnly { get; }

            public static TokenPattern ForPadding(string patternName, string candidateTokens, Padding logicalPadding)
            {
                return new TokenPattern(patternName, candidateTokens, logicalPadding, exactOnly: true);
            }

            public static TokenPattern ForMargin(string patternName, string candidateTokens, Padding logicalPadding)
            {
                return new TokenPattern(patternName, candidateTokens, logicalPadding, exactOnly: true);
            }

            public static TokenPattern ForScalarMargin(string patternName, string candidateTokens, int logicalScalar)
            {
                return new TokenPattern(patternName, candidateTokens, new Padding(logicalScalar), exactOnly: false);
            }

            public bool IsMatch(Padding padding)
            {
                if (MatchesPadding(padding, LogicalPadding) || MatchesPadding(padding, ScaledPadding))
                {
                    return true;
                }

                if (ExactOnly)
                {
                    return false;
                }

                var logicalScalar = LogicalPadding.Left;
                var scaledScalar = ScaledPadding.Left;
                return MatchesScalar(padding.Left, logicalScalar)
                    || MatchesScalar(padding.Top, logicalScalar)
                    || MatchesScalar(padding.Right, logicalScalar)
                    || MatchesScalar(padding.Bottom, logicalScalar)
                    || MatchesScalar(padding.Left, scaledScalar)
                    || MatchesScalar(padding.Top, scaledScalar)
                    || MatchesScalar(padding.Right, scaledScalar)
                    || MatchesScalar(padding.Bottom, scaledScalar);
            }

            private static bool MatchesPadding(Padding actual, Padding expected)
            {
                return MatchesScalar(actual.Left, expected.Left)
                    && MatchesScalar(actual.Top, expected.Top)
                    && MatchesScalar(actual.Right, expected.Right)
                    && MatchesScalar(actual.Bottom, expected.Bottom);
            }

            private static bool MatchesScalar(int actual, int expected)
            {
                return Math.Abs(actual - expected) <= PaddingTolerance;
            }
        }
    }
}
