using System;
using System.Linq;
using FluentAssertions;
using WileyWidget.WinForms.Controls;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.ViewModels.Tests
{
    public class ActivityLogViewModelTests
    {
        [Fact]
        public void AddActivity_InsertsToTop_And_TrimsTo500()
        {
            var vm = new ActivityLogViewModel();

            for (int i = 0; i < 510; i++)
            {
                vm.AddActivity(new ActivityLog { Timestamp = DateTime.UtcNow, Activity = $"a{i}" });
            }

            vm.ActivityEntries.Count.Should().BeLessOrEqualTo(500);
            vm.ActivityEntries.First().Activity.Should().Be("a509");
        }

        [Fact]
        public void ClearActivityLog_ClearsCollection()
        {
            var vm = new ActivityLogViewModel();
            vm.AddActivity(new ActivityLog { Timestamp = DateTime.UtcNow, Activity = "x" });
            vm.ActivityEntries.Should().NotBeEmpty();

            vm.ClearActivityLog();
            vm.ActivityEntries.Should().BeEmpty();
        }
    }
}
