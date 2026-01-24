#nullable enable

using System;
using System.Reflection;
using WileyWidget.WinForms.Controls;

namespace WileyWidget.Tests.TestHelpers;

/// <summary>
/// Test helper methods for working with ScopedPanelBase and derived panels in unit tests.
/// Provides access to private state for testing without exposing it publicly.
/// </summary>
public static class ScopedPanelTestHelpers
{
    /// <summary>
    /// Gets the resolved ViewModel instance from a panel (which is normally private).
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>The resolved ViewModel instance, or null if not yet resolved.</returns>
    /// <remarks>
    /// Uses reflection to access the private _viewModel field.
    /// Returns null if the field is not found or has not been set.
    /// This method is intended for testing only and is not part of the public API.
    /// </remarks>
    public static TViewModel? GetViewModelForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_viewModel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field == null)
                return null;

            var value = field.GetValue(panel);
            return value as TViewModel;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the scoped service provider from a panel for resolving test dependencies.
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>The scoped IServiceProvider, or null if the scope has not been created or has been disposed.</returns>
    /// <remarks>
    /// Uses reflection to access the private _scope field and returns its ServiceProvider.
    /// The scope is created during OnHandleCreated, so this will return null if the panel's handle has not been created.
    /// This method is intended for testing only and is not part of the public API.
    /// </remarks>
    public static IServiceProvider? GetServiceProviderForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_scope",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field == null)
                return null;

            var scope = field.GetValue(panel);
            if (scope == null)
                return null;

            var propertyInfo = scope.GetType().GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Instance);
            return propertyInfo?.GetValue(scope) as IServiceProvider;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Simulates the panel handle creation, which triggers ViewModel resolution and OnViewModelResolved.
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to initialize.</param>
    /// <remarks>
    /// Accessing the Handle property forces WinForms to create the control handle, which triggers OnHandleCreated.
    /// Use this in unit tests to initialize a panel after construction without showing it on screen.
    /// If the handle is already created, this is a no-op.
    /// </remarks>
    public static void SimulateHandleCreation<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        // Access the Handle property to force handle creation
        // This triggers OnHandleCreated internally
        _ = panel.Handle;
    }

    /// <summary>
    /// Gets the validation errors collection from a panel (normally accessed via ICompletablePanel.ValidationErrors).
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>The list of validation errors, or an empty list if none exist or access fails.</returns>
    /// <remarks>
    /// Uses reflection to access the private _validationErrors field.
    /// This provides direct access for test assertions, though the public property ICompletablePanel.ValidationErrors is preferred.
    /// </remarks>
    public static List<ValidationItem> GetValidationErrorsForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_validationErrors",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field == null)
                return new();

            var value = field.GetValue(panel);
            return (value as List<ValidationItem>) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Gets the IsLoaded state from a panel (normally accessed via ICompletablePanel.IsLoaded).
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>True if the panel is loaded; false otherwise.</returns>
    /// <remarks>
    /// Uses reflection to access the private _isLoaded field.
    /// This is provided for advanced testing scenarios; the public property is preferred.
    /// </remarks>
    public static bool GetIsLoadedForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_isLoaded",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            return field != null && (bool)(field.GetValue(panel) ?? false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the HasUnsavedChanges state from a panel (normally accessed via ICompletablePanel.HasUnsavedChanges).
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>True if the panel has unsaved changes; false otherwise.</returns>
    /// <remarks>
    /// Uses reflection to access the private _hasUnsavedChanges field.
    /// This is provided for advanced testing scenarios; the public property is preferred.
    /// </remarks>
    public static bool GetHasUnsavedChangesForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_hasUnsavedChanges",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            return field != null && (bool)(field.GetValue(panel) ?? false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the IsBusy state from a panel (normally accessed via ICompletablePanel.IsBusy).
    /// </summary>
    /// <typeparam name="TPanel">The panel type.</typeparam>
    /// <typeparam name="TViewModel">The ViewModel type.</typeparam>
    /// <param name="panel">The panel instance to inspect.</param>
    /// <returns>True if the panel is currently performing a long-running operation; false otherwise.</returns>
    /// <remarks>
    /// Uses reflection to access the private _isBusy field.
    /// This is provided for advanced testing scenarios; the public property is preferred.
    /// </remarks>
    public static bool GetIsBusyForTesting<TPanel, TViewModel>(TPanel panel)
        where TPanel : ScopedPanelBase<TViewModel>
        where TViewModel : class
    {
        try
        {
            var field = typeof(ScopedPanelBase<TViewModel>).GetField(
                "_isBusy",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            return field != null && (bool)(field.GetValue(panel) ?? false);
        }
        catch
        {
            return false;
        }
    }
}
