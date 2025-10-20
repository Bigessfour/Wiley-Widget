using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Xunit;

namespace WileyWidget.LifecycleTests;

public sealed class AIAssistViewThemeAndBehaviorTests : LifecycleTestBase
{
    [Fact]
    public async Task AIAssistPanelView_UsesThemeResources_And_AppliesFluentTheme()
    {
        await RunOnDispatcherAsync(async () =>
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }

            var view = new AIAssistPanelView();
            view.Width = 800;
            view.Height = 600;
            view.WindowStartupLocation = WindowStartupLocation.Manual;
            view.Left = -32000;
            view.Top = -32000;

            view.ApplyTemplate();
            view.UpdateLayout();

            // Theme must be applied globally; verify Fluent style is active for the view
            var style = SfSkinManager.GetVisualStyle(view);
            Assert.True(style == VisualStyles.FluentDark || style == VisualStyles.FluentLight,
                $"Expected Fluent theme applied, found '{style}'.");

            // Verify critical brushes come from resources (were converted from hard-coded colors)
            var border = view.FindName("UiProbeControl") as Control; // presence indicates tree loaded
            Assert.NotNull(border);

            // Verify panel-level resources exist in app dictionaries
            var panelBrush = Application.Current.TryFindResource("PanelBackgroundBrush");
            var borderBrush = Application.Current.TryFindResource("CardBorderBrush");
            var errorBrush = Application.Current.TryFindResource("ErrorBrush");
            var infoBrush = Application.Current.TryFindResource("InfoBrush");
            Assert.NotNull(panelBrush);
            Assert.NotNull(borderBrush);
            Assert.NotNull(errorBrush);
            Assert.NotNull(infoBrush);

            // Validate InputBindings exist (Ctrl+Enter, Alt+S, Alt+R)
            var hasCtrlEnter = view.InputBindings.OfType<KeyBinding>().Any(k => k.Key == Key.Enter && k.Modifiers == ModifierKeys.Control);
            var hasAltS = view.InputBindings.OfType<KeyBinding>().Any(k => k.Key == Key.S && k.Modifiers == ModifierKeys.Alt);
            var hasAltR = view.InputBindings.OfType<KeyBinding>().Any(k => k.Key == Key.R && k.Modifiers == ModifierKeys.Alt);
            Assert.True(hasCtrlEnter && hasAltS && hasAltR, "Expected keyboard shortcuts Ctrl+Enter, Alt+S, Alt+R to be defined.");

            // Access key underscore in label
            var sendButton = FindDescendants<ContentControl>(view).FirstOrDefault(c => (c as dynamic)?.Label == "_Send Message");
            Assert.NotNull(sendButton);

            // Clean up
            if (view.IsLoaded)
                view.Close();
        });
    }

    [Fact]
    public async Task AIAssistPanelView_ValidationAndSendFlow_Works()
    {
        await RunOnDispatcherAsync(async () =>
        {
            var vm = CreateViewModel();
            var view = new AIAssistPanelView { DataContext = vm };
            view.ApplyTemplate();
            view.UpdateLayout();

            // Initially invalid
            vm.MessageText = string.Empty;
            Assert.False(vm.IsInputValid);

            // Too long
            vm.MessageText = new string('x', 2100);
            Assert.False(vm.IsInputValid);
            Assert.Contains("too long", vm.InputValidationError, StringComparison.OrdinalIgnoreCase);

            // Harmful content is rejected
            vm.MessageText = "please run <script>alert('x')</script>";
            Assert.False(vm.IsInputValid);

            // Valid path
            vm.MessageText = "What is our current surplus?";
            Assert.True(vm.IsInputValid);
            Assert.True(vm.SendMessageCommand.CanExecute());

            // KeyBinding command targets should reflect the VM CanExecute
            var ctrlEnterBinding = view.InputBindings.OfType<KeyBinding>().First(k => k.Key == Key.Enter && k.Modifiers == ModifierKeys.Control);
            Assert.True(ctrlEnterBinding.Command.CanExecute(null));

            // Execute send (uses test AI service via Grok inside VM lifecycle tests infra)
            await vm.SendMessageCommand.ExecuteAsync(null);

            Assert.True(vm.ChatMessages.Count >= 2); // user + assistant

            // DEBUG perf timing will log; ensure processing flags reset
            Assert.False(vm.IsProcessing);
            Assert.False(vm.IsTyping);

            if (view.IsLoaded)
                view.Close();
        });
    }

    private static T[] FindDescendants<T>(DependencyObject root)
    {
        if (root == null) return Array.Empty<T>();
        var results = new System.Collections.Generic.List<T>();
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) results.Add(t);
            results.AddRange(FindDescendants<T>(child));
        }
        return results.ToArray();
    }

    private AIAssistViewModel CreateViewModel()
    {
        // Use existing lifecycle test helpers to create dispatcher and fakes
        var dispatcher = CreateDispatcherHelper();
        var repository = new WileyWidget.Data.EnterpriseRepository(DbContextFactory);
        var ai = new StubAIService();
        var charge = new StubChargeCalculatorService();
        var scenario = new StubScenarioEngine();
        var grok = new WileyWidget.Services.GrokSupercomputer(ai, repository, CreateLogger<WileyWidget.Services.GrokSupercomputer>());

        return new AIAssistViewModel(ai, charge, scenario, grok, repository, dispatcher, CreateLogger<AIAssistViewModel>());
    }

    private sealed class StubAIService : WileyWidget.Services.IAIService
    {
        public Task<string> AnalyzeDataAsync(string data, string analysisType, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult("analysis");
        public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult("mock");
        public Task<string> GetInsightsAsync(string context, string question, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult("assistant response");
        public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult("review");
    }

    private sealed class StubChargeCalculatorService : WileyWidget.Business.Interfaces.IChargeCalculatorService
    {
        public Task<WileyWidget.Models.ServiceChargeRecommendation> CalculateRecommendedChargeAsync(int enterpriseId) => Task.FromResult(new WileyWidget.Models.ServiceChargeRecommendation());
        public Task<WileyWidget.Models.WhatIfScenario> GenerateChargeScenarioAsync(int enterpriseId, decimal proposedRateIncrease, decimal proposedExpenseChange = 0) => Task.FromResult(new WileyWidget.Models.WhatIfScenario());
    }

    private sealed class StubScenarioEngine : WileyWidget.Business.Interfaces.IWhatIfScenarioEngine
    {
        public Task<WileyWidget.Models.ComprehensiveScenario> GenerateComprehensiveScenarioAsync(int enterpriseId, WileyWidget.Models.ScenarioParameters parameters) => Task.FromResult(new WileyWidget.Models.ComprehensiveScenario());
    }
}
