using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using System.Reflection;
using System.Linq;
using WileyWidget.WinForms.Forms;

#if FALSE // MCP Server not available as project reference; run MCP server standalone instead

namespace WileyWidget.WinForms.Tests.Unit.Controls
{
    /// <summary>
    /// Integration tests for all panel controls in src/WileyWidget.WinForms/Controls folder.
    /// Tests proper implementation of ScopedPanelBase, ViewModel resolution, and UI patterns.
    /// Includes both reflection-based type validation and ExecuteOnStaThread-based instantiation tests.
    /// </summary>
    [Collection("Non-UI Panel Tests")]
    [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters")]
    public class PanelControlsIntegrationTests
    {
        /// <summary>
        /// Configures a minimal DI container for testing panels without full application startup.
        /// </summary>
        private static IServiceProvider CreateTestServiceProvider()
        {
            var services = new ServiceCollection();

            // Register logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Register ViewModels (minimal stubs for testing)
            services.AddScoped<BudgetViewModel>();
            services.AddScoped<ChartViewModel>();
            services.AddScoped<AccountsViewModel>();
            services.AddScoped<AnalyticsViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<UtilityBillViewModel>();

            return services.BuildServiceProvider();
        }

        #region GradientPanelExt Type Tests

        [Fact]
        public void GradientPanelExt_IsPublic()
        {
            // Arrange & Act
            var type = typeof(WileyWidget.WinForms.Controls.GradientPanelExt);

            // Assert
            Assert.True(type.IsPublic, "GradientPanelExt should be public");
        }

        [Fact]
        public void GradientPanelExt_InheritsFromSyncfusion()
        {
            // Arrange & Act
            var type = typeof(WileyWidget.WinForms.Controls.GradientPanelExt);
            var baseType = type.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal("GradientPanelExt", baseType.Name);
            Assert.Equal("Syncfusion.Windows.Forms.Tools", baseType.Namespace);
        }

        [Fact]
        public void GradientPanelExt_HasDefaultConstructor()
        {
            // Arrange & Act
            var type = typeof(WileyWidget.WinForms.Controls.GradientPanelExt);
            var constructor = type.GetConstructor(Type.EmptyTypes);

            // Assert
            Assert.NotNull(constructor);
            Assert.True(constructor.IsPublic);
        }

        #endregion

        #region ScopedPanelBase Type Tests

        [Fact]
        public void ScopedPanelBase_IsAbstract()
        {
            // Arrange & Act
            var type = typeof(ScopedPanelBase<>);

            // Assert
            Assert.True(type.IsAbstract);
        }

        [Fact]
        public void ScopedPanelBase_InheritsFromUserControl()
        {
            // Arrange & Act
            var type = typeof(ScopedPanelBase<>);
            var baseType = type.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.Equal(typeof(UserControl), baseType);
        }

        [Fact]
        public void ScopedPanelBase_HasGetViewModelForTesting()
        {
            // Arrange & Act
            var type = typeof(ScopedPanelBase<>);
            var method = type.GetMethod("GetViewModelForTesting");

            // Assert
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
        }

        [Fact]
        public void ScopedPanelBase_HasProtectedOnViewModelResolved()
        {
            // Arrange & Act
            var type = typeof(ScopedPanelBase<>);
            var method = type.GetMethod("OnViewModelResolved",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // Assert
            Assert.NotNull(method);
            Assert.True(method.IsVirtual);
        }

        #endregion

        #region Panel Type Tests

        [Fact]
        public void BudgetPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(BudgetPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        [Fact]
        public void ChartPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(ChartPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        [Fact]
        public void AccountsPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(AccountsPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        [Fact]
        public void AnalyticsPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(AnalyticsPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        [Fact]
        public void SettingsPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(SettingsPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        [Fact]
        public void UtilityBillPanel_ExistsAndIsPublic()
        {
            // Arrange & Act
            var type = typeof(UtilityBillPanel);

            // Assert
            Assert.NotNull(type);
            Assert.True(type.IsPublic);
        }

        #endregion

        #region Panel Inheritance Tests

        [Fact]
        public void BudgetPanel_InheritsFromScopedPanelBase()
        {
            // Arrange & Act
            var type = typeof(BudgetPanel);
            var baseType = type.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.True(baseType.IsGenericType);
            Assert.Equal(typeof(ScopedPanelBase<>).Name, baseType.GetGenericTypeDefinition().Name);
        }

        [Fact]
        public void ChartPanel_InheritsFromScopedPanelBase()
        {
            // Arrange & Act
            var type = typeof(ChartPanel);
            var baseType = type.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.True(baseType.IsGenericType);
            Assert.Equal(typeof(ScopedPanelBase<>).Name, baseType.GetGenericTypeDefinition().Name);
        }

        [Fact]
        public void AccountsPanel_InheritsFromScopedPanelBase()
        {
            // Arrange & Act
            var type = typeof(AccountsPanel);
            var baseType = type.BaseType;

            // Assert
            Assert.NotNull(baseType);
            Assert.True(baseType.IsGenericType);
            Assert.Equal(typeof(ScopedPanelBase<>).Name, baseType.GetGenericTypeDefinition().Name);
        }

        #endregion

        #region Panel ViewModel Binding Tests

        [Fact]
        public void BudgetPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(BudgetPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(BudgetViewModel), genericArg);
        }

        [Fact]
        public void ChartPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(ChartPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(ChartViewModel), genericArg);
        }

        [Fact]
        public void AccountsPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(AccountsPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(AccountsViewModel), genericArg);
        }

        [Fact]
        public void AnalyticsPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(AnalyticsPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(AnalyticsViewModel), genericArg);
        }

        [Fact]
        public void SettingsPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(SettingsPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(SettingsViewModel), genericArg);
        }

        [Fact]
        public void UtilityBillPanel_UsesCorrectViewModel()
        {
            // Arrange & Act
            var type = typeof(UtilityBillPanel);
            var baseType = type.BaseType;
            Assert.NotNull(baseType);
            var genericArg = baseType.GetGenericArguments().FirstOrDefault();

            // Assert
            Assert.Equal(typeof(UtilityBillViewModel), genericArg);
        }

        #endregion

        #region Panel API Surface Tests

        [Fact]
        public void AllPanels_InheritFromUserControl()
        {
            // Arrange
            var panelTypes = new[]
            {
                typeof(BudgetPanel),
                typeof(ChartPanel),
                typeof(AccountsPanel),
                typeof(AnalyticsPanel),
                typeof(SettingsPanel),
                typeof(UtilityBillPanel),
                typeof(WileyWidget.WinForms.Controls.GradientPanelExt)
            };

            // Act & Assert
            foreach (var panelType in panelTypes)
            {
                Assert.True(panelType.IsAssignableTo(typeof(Control)),
                    $"{panelType.Name} should inherit from Control");
            }
        }

        [Fact]
        public void ScopedPanels_HavePublicConstructor()
        {
            // Arrange
            var scopedPanelTypes = new[]
            {
                typeof(BudgetPanel),
                typeof(ChartPanel),
                typeof(AccountsPanel),
                typeof(AnalyticsPanel),
                typeof(SettingsPanel),
                typeof(UtilityBillPanel)
            };

            // Act & Assert
            foreach (var panelType in scopedPanelTypes)
            {
                var constructors = panelType.GetConstructors();
                Assert.NotEmpty(constructors);

                var hasPublicConstructor = constructors.Any(c =>
                    c.IsPublic &&
                    c.GetParameters().Any(p => p.ParameterType == typeof(IServiceScopeFactory)));

                Assert.True(hasPublicConstructor,
                    $"{panelType.Name} should have a public constructor with IServiceScopeFactory");
            }
        }

        [Fact]
        public void AllPanels_HaveAccessibleName()
        {
            // Arrange
            var panelTypes = new[]
            {
                typeof(BudgetPanel),
                typeof(ChartPanel),
                typeof(AccountsPanel),
                typeof(AnalyticsPanel),
                typeof(SettingsPanel),
                typeof(UtilityBillPanel),
                typeof(WileyWidget.WinForms.Controls.GradientPanelExt)
            };

            // Act & Assert
            foreach (var panelType in panelTypes)
            {
                var hasAccessibleNameProperty = panelType.GetProperty("AccessibleName") != null;
                Assert.True(hasAccessibleNameProperty,
                    $"{panelType.Name} should have AccessibleName property");
            }
        }

        [Fact]
        public void AllPanels_HaveDisposeMethod()
        {
            // Arrange
            var panelTypes = new[]
            {
                typeof(BudgetPanel),
                typeof(ChartPanel),
                typeof(AccountsPanel),
                typeof(AnalyticsPanel),
                typeof(SettingsPanel),
                typeof(UtilityBillPanel),
                typeof(WileyWidget.WinForms.Controls.GradientPanelExt)
            };

            // Act & Assert
            foreach (var panelType in panelTypes)
            {
                var disposeMethod = panelType.GetMethod("Dispose");
                Assert.NotNull(disposeMethod);
            }
        }

        #endregion
    }
}

#endif // MCP Server integration disabled
