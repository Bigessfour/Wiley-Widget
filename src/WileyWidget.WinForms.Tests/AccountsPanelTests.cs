#nullable enable

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.Models;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Windows.Threading;

namespace WileyWidget.WinForms.Tests
{
    public class AccountsPanelTests
    {
        [Test]
        public void Ctor_WithNullVM_Throws()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentNullException>(() => Activator.CreateInstance(typeof(AccountsPanel), new object?[] { null }));
        }

        [Test]
        public void Load_AsyncSuccess_BindsGrid()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            // Act
            using var panel = new AccountsPanel(vm);

            // Access private field gridAccounts
            var gridField = typeof(AccountsPanel).GetField("gridAccounts", BindingFlags.Instance | BindingFlags.NonPublic);
            var grid = gridField?.GetValue(panel);

            // Assert
            grid.Should().NotBeNull("gridAccounts should be created and bound in constructor");

            // The DataSource should be the same ObservableCollection instance on the viewmodel
            var dataSourceProp = grid?.GetType().GetProperty("DataSource");
            var ds = dataSourceProp?.GetValue(grid);
            ds.Should().BeSameAs(vm.Accounts);
        }

        [Test]
        public void ComboFund_Validating_NoCancel()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            using var panel = new AccountsPanel(vm);

            var comboField = typeof(AccountsPanel).GetField("comboFund", BindingFlags.Instance | BindingFlags.NonPublic);
            var combo = comboField?.GetValue(panel);

            // Validate there's a combo and an error provider
            combo.Should().NotBeNull();

            var validatingMethod = typeof(AccountsPanel).GetMethod("ComboFund_Validating", BindingFlags.Instance | BindingFlags.NonPublic);
            var validatedMethod = typeof(AccountsPanel).GetMethod("ComboFund_Validated", BindingFlags.Instance | BindingFlags.NonPublic);


            var args = new CancelEventArgs();

            // Act
            validatingMethod?.Invoke(panel, new object?[] { combo, args });

            // Assert no cancellation
            args.Cancel.Should().BeFalse();

            // Ensure validated clears errors (should not throw)
            validatedMethod?.Invoke(panel, new object?[] { combo, EventArgs.Empty });

            var errProvField = typeof(AccountsPanel).GetField("errorProvider", BindingFlags.Instance | BindingFlags.NonPublic);
            var errProv = errProvField?.GetValue(panel);

            // If available, GetError should return empty string after validated
            if (errProv != null)
            {
                var getError = errProv!.GetType().GetMethod("GetError");
                var errText = getError?.Invoke(errProv, new object?[] { combo });
                errText.Should().Be(string.Empty);
            }
        }

        [Apartment(ApartmentState.STA)]
        public async Task PropertyChanged_InvokeRequired_Marshals()
        {
            // Arrange a view model and a panel which uses a fake dispatcher to force marshaling
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            // Create a fake dispatcher that will force invocation of callbacks
            var fakeDispatcher = new TestableDispatcher
            {
                CheckAccessImpl = () => false, // force marshal
                InvokeAsyncImpl = (action) => { action(); return Task.CompletedTask; }
            };

            using var panel = new AccountsPanel(vm, fakeDispatcher);

            var labelField = typeof(AccountsPanel).GetField("lblTotalBalance", BindingFlags.Instance | BindingFlags.NonPublic);
            labelField.Should().NotBeNull();
            var label = (System.Windows.Forms.Label)labelField!.GetValue(panel)!;

            // Act: change viewmodel property which will raise PropertyChanged
            vm.TotalBalance = 12345.67m;

            // Wait briefly to ensure the UI handler had a chance to run
            await Task.Delay(50);

            // Assert
            label.Text.Should().Contain(vm.TotalBalance.ToString("N2", CultureInfo.InvariantCulture));
        }

        [Test]
        public void Integration_MultipleAccounts_SummaryUpdatedAndNoCrashOnEmpty()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            // Populate accounts collection manually (no DB calls)
            vm.Accounts.Clear();
            for (int i = 1; i <= 5; i++)
            {
                vm.Accounts.Add(new MunicipalAccountDisplay { Id = i, AccountName = $"Acct {i}", AccountNumber = $"{1000 + i}", CurrentBalance = i * 100m, IsActive = true });
            }

            vm.TotalBalance = vm.Accounts.Sum(a => a.CurrentBalance);
            vm.ActiveAccountCount = vm.Accounts.Count;

            using var panel = new AccountsPanel(vm);

            var lblTotalField = typeof(AccountsPanel).GetField("lblTotalBalance", BindingFlags.Instance | BindingFlags.NonPublic);
            var lblCountField = typeof(AccountsPanel).GetField("lblAccountCount", BindingFlags.Instance | BindingFlags.NonPublic);

            var lblTotal = (System.Windows.Forms.Label)lblTotalField!.GetValue(panel)!;
            var lblCount = (System.Windows.Forms.Label)lblCountField!.GetValue(panel)!;

            lblTotal.Text.Should().Contain(vm.TotalBalance.ToString("C2", CultureInfo.InvariantCulture));
            lblCount.Text.Should().Contain(vm.ActiveAccountCount.ToString(CultureInfo.InvariantCulture));

            // Empty data should not crash
            vm.Accounts.Clear();
            vm.TotalBalance = 0m;
            vm.ActiveAccountCount = 0;

            Assert.DoesNotThrow(() => panel.Dispose());
        }

        [Test]
        public void Syncfusion_Grid_AllowFilteringTrue()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            using var panel = new AccountsPanel(vm);

            var gridField = typeof(AccountsPanel).GetField("gridAccounts", BindingFlags.Instance | BindingFlags.NonPublic);
            var grid = gridField?.GetValue(panel);

            var prop = grid?.GetType().GetProperty("ShowFilterBar");
            var val = prop?.GetValue(grid);
            val.Should().Be(true);
        }

        public class TestableDispatcher : WileyWidget.Services.Threading.IDispatcherHelper
        {
            public Func<bool>? CheckAccessImpl { get; set; }
            public Func<Action, Task>? InvokeAsyncImpl { get; set; }

            public bool CheckAccess() => CheckAccessImpl?.Invoke() ?? true;
            public void Invoke(Action action) => action();
            public T Invoke<T>(Func<T> func) => func();
            public Task InvokeAsync(Action action) => (InvokeAsyncImpl ?? (act => { act(); return Task.CompletedTask; }))(action);
            public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());

            // Overloads with DispatcherPriority were added to the IDispatcherHelper interface;
            // provide passthrough implementations for tests so priority isn't relevant here.
            public Task InvokeAsync(Action action, DispatcherPriority priority) => InvokeAsync(action);
            public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority) => InvokeAsync(func);
        }
    }
}
