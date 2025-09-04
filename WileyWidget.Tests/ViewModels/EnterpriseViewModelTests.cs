using Xunit;
using FluentAssertions;
using System.Threading.Tasks;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Tests.ViewModels;

public class EnterpriseViewModelTests
{
	private class InMemoryEnterpriseRepository : IEnterpriseRepository
	{
		private readonly List<Enterprise> _items = new();
		private int _nextId = 1;

		public Task<Enterprise> AddAsync(Enterprise enterprise)
		{
			enterprise.Id = _nextId++;
			_items.Add(enterprise);
			return Task.FromResult(enterprise);
		}

		public Task<bool> DeleteAsync(int id)
		{
			var e = _items.FirstOrDefault(x => x.Id == id);
			if (e == null) return Task.FromResult(false);
			_items.Remove(e);
			return Task.FromResult(true);
		}

		public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) =>
			Task.FromResult(_items.Any(x => x.Name == name && (!excludeId.HasValue || x.Id != excludeId.Value)));

		public Task<IEnumerable<Enterprise>> GetAllAsync() => Task.FromResult<IEnumerable<Enterprise>>(_items.ToList());
		public Task<int> GetCountAsync() => Task.FromResult(_items.Count);
		public Task<Enterprise> GetByIdAsync(int id) => Task.FromResult(_items.First(x => x.Id == id));
		public Task<Enterprise> GetByNameAsync(string name) => Task.FromResult(_items.First(x => x.Name == name));
		public Task<IEnumerable<Enterprise>> GetWithInteractionsAsync() => Task.FromResult<IEnumerable<Enterprise>>(_items.ToList());
		public Task<Enterprise> UpdateAsync(Enterprise enterprise)
		{
			var existing = _items.First(x => x.Id == enterprise.Id);
			existing.Name = enterprise.Name;
			existing.CurrentRate = enterprise.CurrentRate;
			existing.MonthlyExpenses = enterprise.MonthlyExpenses;
			existing.CitizenCount = enterprise.CitizenCount;
			return Task.FromResult(existing);
		}
	}

	private class TestEditorDialog : WileyWidget.UI.Dialogs.IEnterpriseEditorDialog
	{
		private readonly Enterprise _result;
		public TestEditorDialog(Enterprise result) { _result = result; }
		public Enterprise Show(Enterprise enterprise) => _result;
	}

	[Fact]
	public async Task AddEnterpriseAsync_ShouldValidateUniqueName()
	{
		var repo = new InMemoryEnterpriseRepository();
		var dialog = new TestEditorDialog(new Enterprise { Name = "Test", CurrentRate = 10m });
		using (var vm = new EnterpriseViewModel(repo, dialog))
		{
			await vm.AddEnterpriseAsync();
			await vm.AddEnterpriseAsync(); // second attempt with same name
			(await repo.GetCountAsync()).Should().Be(1); // second add blocked
		}
	}

	[Fact]
	public async Task SaveEnterpriseAsync_ShouldPreventNameConflict()
	{
		var repo = new InMemoryEnterpriseRepository();
		var dialog1 = new TestEditorDialog(new Enterprise { Name = "A" });
		using (var vm = new EnterpriseViewModel(repo, dialog1))
		{
			await vm.AddEnterpriseAsync();
		}

		var dialog2 = new TestEditorDialog(new Enterprise { Name = "B" });
		using (var vm2 = new EnterpriseViewModel(repo, dialog2))
		{
			await vm2.AddEnterpriseAsync();
			vm2.SelectedEnterprise = vm2.Enterprises.First();
			vm2.SelectedEnterprise.Name = "A"; // conflict
			await vm2.SaveEnterpriseAsync();
		}

		var all = await repo.GetAllAsync();
		all.Count(e => e.Name == "A").Should().Be(1); // still only one 'A'
	}

	[Fact]
	public async Task DeleteEnterpriseAsync_RemovesItem()
	{
		var repo = new InMemoryEnterpriseRepository();
		var dialog = new TestEditorDialog(new Enterprise { Name = "Del" });
		using (var vm = new EnterpriseViewModel(repo, dialog))
		{
			await vm.AddEnterpriseAsync();
			vm.SelectedEnterprise = vm.Enterprises.First();
			await vm.DeleteEnterpriseAsync();
		}
		(await repo.GetCountAsync()).Should().Be(0);
	}
}
