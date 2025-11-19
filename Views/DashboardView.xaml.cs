using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WileyWidget.ViewModels;
using Serilog;

namespace WileyWidget.Views
{
    /// <summary>
    /// QuickBooks Dashboard landing page displaying connection status and financial KPIs.
    /// </summary>
    public sealed partial class DashboardView : UserControl
    {
        public QuickBooksDashboardViewModel ViewModel { get; }

        public DashboardView()
        {
            Log.Information("DashboardView constructor called");
            
            // Get ViewModel from DI container
            ViewModel = App.Services?.GetService(typeof(QuickBooksDashboardViewModel)) as QuickBooksDashboardViewModel
                ?? throw new System.InvalidOperationException("QuickBooksDashboardViewModel not registered in DI container");
            
            this.InitializeComponent();
            this.DataContext = ViewModel;
            
            // Subscribe to property changes to trigger animations
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            Log.Information("DashboardView initialized with ViewModel");
            
            // Load data when view is created
            _ = ViewModel.LoadAsync();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Trigger animations when data finishes loading
            if (e.PropertyName == nameof(ViewModel.HasData) && ViewModel.HasData)
            {
                Log.Information("Data loaded, triggering card entrance animations");
                AnimateCardsEntrance();
            }
        }

        /// <summary>
        /// Animate KPI cards with staggered fade-in and slide-up effect.
        /// </summary>
        private void AnimateCardsEntrance()
        {
            try
            {
                var cards = new UIElement[] { RevenueCard, NetIncomeCard, ARCard, APCard };
                var delayIncrement = 100; // Stagger delay in milliseconds

                for (int i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    if (card == null) continue;

                    // Set initial state
                    card.Opacity = 0;
                    
                    // Get or create CompositeTransform
                    if (card.RenderTransform is not CompositeTransform transform)
                    {
                        transform = new CompositeTransform();
                        card.RenderTransform = transform;
                    }
                    transform.TranslateY = 30;

                    // Create storyboard for this card
                    var storyboard = new Storyboard();
                    var beginTime = TimeSpan.FromMilliseconds(i * delayIncrement);

                    // Fade in animation
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        BeginTime = beginTime,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(fadeIn, card);
                    Storyboard.SetTargetProperty(fadeIn, "Opacity");
                    storyboard.Children.Add(fadeIn);

                    // Slide up animation
                    var slideUp = new DoubleAnimation
                    {
                        From = 30,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        BeginTime = beginTime,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(slideUp, card);
                    Storyboard.SetTargetProperty(slideUp, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
                    storyboard.Children.Add(slideUp);

                    // Start the animation
                    storyboard.Begin();
                }

                Log.Information("Card entrance animations started successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to animate card entrance");
            }
        }
    }
}
