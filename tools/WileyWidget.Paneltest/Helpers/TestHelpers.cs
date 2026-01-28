using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.Paneltest.Helpers;

/// <summary>
/// Scope factory wrapper for testing panels with DI.
/// Maintains service provider and scope lifetime.
/// </summary>
public class TestScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;

    public TestScopeFactory(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IServiceScope CreateScope()
    {
        return _provider.CreateScope();
    }
}

/// <summary>
/// Reflection helper for accessing private panel state and controls.
/// </summary>
public static class PanelReflectionHelper
{
    /// <summary>
    /// Get the ViewModel from a ScopedPanelBase<T> by reflection.
    /// </summary>
    public static object? GetViewModelForTesting(UserControl panel)
    {
        var vmProperty = panel.GetType().GetProperty("ViewModel", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        return vmProperty?.GetValue(panel);
    }

    /// <summary>
    /// Get a private field value from a panel by name.
    /// </summary>
    public static object? GetPrivateField(UserControl panel, string fieldName)
    {
        var field = panel.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(panel);
    }

    /// <summary>
    /// Get a private property value from a panel by name.
    /// </summary>
    public static object? GetPrivateProperty(UserControl panel, string propertyName)
    {
        var property = panel.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return property?.GetValue(panel);
    }

    /// <summary>
    /// Set a private property on a panel by name.
    /// </summary>
    public static void SetPrivateProperty(UserControl panel, string propertyName, object? value)
    {
        var property = panel.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        property?.SetValue(panel, value);
    }

    /// <summary>
    /// Force WinForms handle creation (triggers OnHandleCreated, DI resolution).
    /// </summary>
    public static void SimulateHandleCreation(UserControl panel)
    {
        var form = new Form();
        try
        {
            form.Controls.Add(panel);
            _ = panel.Handle;
            Application.DoEvents();
        }
        finally
        {
            form.Dispose();
        }
    }
}

/// <summary>
/// Utilities for running UI test code on STA thread (required for WinForms).
/// </summary>
public static class StaThreadRunner
{
    /// <summary>
    /// Run an action on STA thread.
    /// </summary>
    public static void Run(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
        }
        else
        {
            var thread = new Thread(() => action());
            thread.TrySetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }

    /// <summary>
    /// Run a function on STA thread and return result.
    /// </summary>
    public static T Run<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return func();
        }
        else
        {
            T result = default!;
            var thread = new Thread(() => result = func());
            thread.TrySetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }
    }
}
