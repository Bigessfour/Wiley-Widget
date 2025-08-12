using WileyWidget.ViewModels;
using WileyWidget.Models;

namespace WileyWidget.Tests;

public class MainViewModelTests
{
    [Test]
    public void Widgets_Should_Have_Seed_Data()
    {
        var vm = new MainViewModel();
        Assert.That(vm.Widgets, Is.Not.Null);
        Assert.That(vm.Widgets.Count, Is.GreaterThan(0));
    }

    [Test]
    public void First_Widget_Should_Be_Alpha()
    {
        var vm = new MainViewModel();
        Assert.That(vm.Widgets[0].Name, Is.EqualTo("Alpha"));
    }

    [Test]
    public void SelectNext_Should_Set_SelectedWidget_When_Null()
    {
        var vm = new MainViewModel();
        Assert.That(GetSelected(vm), Is.Null);
        vm.SelectNextCommand.Execute(null);
        Assert.That(GetSelected(vm), Is.Not.Null);
        Assert.That(GetSelected(vm).Name, Is.EqualTo("Alpha"));
    }

    [Test]
    public void SelectNext_Should_Cycle()
    {
        var vm = new MainViewModel();
        vm.SelectNextCommand.Execute(null); // Alpha
        vm.SelectNextCommand.Execute(null); // Beta
        vm.SelectNextCommand.Execute(null); // Gamma
        vm.SelectNextCommand.Execute(null); // Wrap -> Alpha
        Assert.That(GetSelected(vm).Name, Is.EqualTo("Alpha"));
    }

    private static Widget GetSelected(MainViewModel vm)
    {
        // Access generated property backing field via reflection since nullable disabled; property exists as SelectedWidget
        var prop = typeof(MainViewModel).GetProperty("SelectedWidget");
        return (Widget)prop.GetValue(vm);
    }
}

public class WidgetModelTests
{
    [Test]
    public void Can_Set_And_Get_Properties()
    {
        var w = new Widget
        {
            Id = 10,
            Name = "Test",
            Category = "Sample",
            Price = 12.34
        };
        Assert.Multiple(() =>
        {
            Assert.That(w.Id, Is.EqualTo(10));
            Assert.That(w.Name, Is.EqualTo("Test"));
            Assert.That(w.Category, Is.EqualTo("Sample"));
            Assert.That(w.Price, Is.EqualTo(12.34));
        });
    }
}
