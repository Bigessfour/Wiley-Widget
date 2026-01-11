using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.WinForms.DataGrid;
using WileyWidget.WinForms.Extensions;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms
{
    [Trait("Category", "Unit")]
    [Collection(WinFormsUiCollection.CollectionName)]
    public class GridExtensionsTests
    {
        private readonly WinFormsUiThreadFixture _ui;

        public GridExtensionsTests(WinFormsUiThreadFixture ui)
        {
            _ui = ui;
        }

        private record Person(string Name, int Age);

        [Fact]
        public void SortByColumn_AddsSortDescription()
        {
            _ui.Run(() =>
            {
                using var grid = new SfDataGrid();
                var data = new[] { new Person("Alice", 30), new Person("Bob", 25), new Person("Carol", 35) };
                grid.DataSource = data.ToList();

                grid.SortByColumn("Age", descending: true);

                Assert.NotEmpty(grid.SortColumnDescriptions);
                Assert.Equal("Age", grid.SortColumnDescriptions[0].ColumnName);
                Assert.Equal(System.ComponentModel.ListSortDirection.Descending, grid.SortColumnDescriptions[0].SortDirection);
            });
        }

        [Fact]
        public void ApplyTextContainsFilter_FiltersDataSource_AndRestoreWorks()
        {
            _ui.Run(() =>
            {
                using var grid = new SfDataGrid();
                var data = new[] { new Person("Budget Alpha", 1), new Person("Other Beta", 2), new Person("Budget Gamma", 3) };
                grid.DataSource = data.ToList();

                grid.ApplyTextContainsFilter("Name", "Budget");

                var filtered = ((IEnumerable<object>)grid.DataSource).ToList();
                Assert.True(filtered.Count < data.Length);

                // Restore
                grid.RestoreOriginalDataSource();
                Assert.NotNull(grid.DataSource);
            });
        }
    }
}
