#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WileyWidget.WinForms.Controls;
using WileyWidget.WinForms.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models.Entities;
using WileyWidget.Models;
using System.Threading;

namespace WileyWidget.WinForms.Tests
{
    public class AccountEditPanelTests
    {
        [Test]
        public void Ctor_New_SetsDataContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            using var panel = new AccountEditPanel(vm);
            panel.DataContext.Should().Be(vm);
        }

        [Apartment(ApartmentState.STA)]
        public async Task BtnSave_Click_Valid_SetsOK()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

            // Seed DB with a department and active budget period
            using (var ctx = new AppDbContext(options))
            {
                ctx.Departments.Add(new Department { Id = 1, Name = "TestDept" });
                ctx.BudgetPeriods.Add(new BudgetPeriod { Id = 1, Year = 2025, Name = "2025", IsActive = true });
                ctx.SaveChanges();
            }

            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            using var panel = new AccountEditPanel(vm);
            using var host = new System.Windows.Forms.Form();
            host.Controls.Add(panel);

            // Set controls directly
            var txtAccount = typeof(AccountEditPanel).GetField("txtAccountNumber", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel) as System.Windows.Forms.TextBox;
            var txtName = typeof(AccountEditPanel).GetField("txtName", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel) as System.Windows.Forms.TextBox;
            var cmbDept = typeof(AccountEditPanel).GetField("cmbDepartment", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel);
            var cmbFund = typeof(AccountEditPanel).GetField("cmbFund", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel);
            var cmbType = typeof(AccountEditPanel).GetField("cmbType", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel);
            var numBalance = typeof(AccountEditPanel).GetField("numBalance", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel) as Syncfusion.WinForms.Input.SfNumericTextBox;
            var numBudget = typeof(AccountEditPanel).GetField("numBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel) as Syncfusion.WinForms.Input.SfNumericTextBox;

            txtAccount!.Text = "A100";
            txtName!.Text = "Some Account";

            // Ensure LoadDataAsync has run (constructor no longer kicks it off)
            var loadMethod = typeof(AccountEditPanel).GetMethod("LoadDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var loadTask = (System.Threading.Tasks.Task)loadMethod.Invoke(panel, null)!;
            await loadTask;

            // Set department selected value
            var deptType = cmbDept!.GetType();
            var dsProp = deptType.GetProperty("DataSource");
            var ds = dsProp!.GetValue(cmbDept) as System.Collections.IList;
            var selectedDept = ds?.Cast<object>().FirstOrDefault();
            // If the combo wasn't populated, create a new one and set SelectedItem
            if (selectedDept == null)
            {
                // fallback: create department object
                selectedDept = new Department { Id = 1, Name = "TestDept" };
                var selProp = deptType.GetProperty("SelectedItem");
                selProp!.SetValue(cmbDept, selectedDept);
            }
            else
            {
                var selProp = deptType.GetProperty("SelectedItem");
                selProp!.SetValue(cmbDept, selectedDept);
            }

            // Select fund and type
            var setSel = cmbFund!.GetType().GetProperty("SelectedItem");
            setSel!.SetValue(cmbFund, Enum.GetValues(typeof(WileyWidget.Models.MunicipalFundType)).GetValue(0));
            var setType = cmbType!.GetType().GetProperty("SelectedItem");
            setType!.SetValue(cmbType, Enum.GetValues(typeof(WileyWidget.Models.AccountType)).GetValue(0));

            numBalance!.Value = 100;
            numBudget!.Value = 200;

            // Invoke private BtnSave_Click on the panel (hosted by a form) so it will set parent's DialogResult
            var saveMethod = typeof(AccountEditPanel).GetMethod("BtnSave_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
            saveMethod.Invoke(panel, new object?[] { null, EventArgs.Empty });

            // Allow async save to run
            await Task.Delay(300);

            // After a successful save the host form/dialog should reflect DialogResult.OK
            host.DialogResult.Should().Be(System.Windows.Forms.DialogResult.OK);
        }

        [Test]
        public async Task LoadDataAsync_PopulatesCombos()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            using (var ctx = new AppDbContext(options))
            {
                ctx.Departments.Add(new Department { Id = 2, Name = "Dept2" });
                ctx.SaveChanges();
            }

            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            using var panel = new AccountEditPanel(vm);

            // Ensure LoadDataAsync has run (constructor no longer kicks it off)
            var loadMethod = typeof(AccountEditPanel).GetMethod("LoadDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var loadTask = (System.Threading.Tasks.Task)loadMethod.Invoke(panel, null)!;
            await loadTask;

            var cmbDept = typeof(AccountEditPanel).GetField("cmbDepartment", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel);
            var prop = cmbDept!.GetType().GetProperty("DataSource");
            var ds = prop!.GetValue(cmbDept) as System.Collections.IList;
            ds.Should().NotBeNull();
            ds!.Count.Should().BeGreaterThan(0);
        }

        [Test]
        public void Dispose_ClearsDataSource_NoNRE()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var factory = new AppDbContextFactory(options);
            var vm = new AccountsViewModel(NullLogger<AccountsViewModel>.Instance, factory);

            var panel = new AccountEditPanel(vm);
            // Call dispose which should not throw (clears data sources safely)
            Assert.DoesNotThrow(() => panel.Dispose());
        }
    }
}
