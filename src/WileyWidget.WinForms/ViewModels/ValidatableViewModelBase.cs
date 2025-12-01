using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Base class for ViewModels that require validation support via <see cref="INotifyDataErrorInfo"/>.
    /// Combines <see cref="ObservableValidator"/> functionality with <see cref="IMessenger"/> support
    /// for ViewModels that need both validation and messaging capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since <see cref="ObservableValidator"/> and <see cref="ObservableRecipient"/> both inherit from
    /// <see cref="ObservableObject"/>, we cannot use multiple inheritance. This base class inherits from
    /// <see cref="ObservableValidator"/> and manually integrates <see cref="IMessenger"/> support.
    /// </para>
    /// <para>
    /// Use this base class for ViewModels with user-editable properties that require validation.
    /// For read-only display ViewModels, continue using <see cref="ObservableRecipient"/>.
    /// </para>
    /// </remarks>
    public abstract class ValidatableViewModelBase : ObservableValidator, IRecipient<object>
    {
        private IMessenger? _messenger;
        private bool _isActive;

        /// <summary>
        /// Gets or sets the <see cref="IMessenger"/> instance used for messaging.
        /// </summary>
        protected IMessenger Messenger
        {
            get => _messenger ?? WeakReferenceMessenger.Default;
            set => _messenger = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this recipient is currently active
        /// (i.e., registered with the messenger).
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value, false))
                {
                    if (value)
                    {
                        OnActivated();
                    }
                    else
                    {
                        OnDeactivated();
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatableViewModelBase"/> class.
        /// </summary>
        protected ValidatableViewModelBase() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance with a custom validation context.
        /// </summary>
        /// <param name="validationContext">The validation context to use.</param>
        protected ValidatableViewModelBase(ValidationContext validationContext) : base(validationContext)
        {
        }

        /// <summary>
        /// Initializes a new instance with a custom messenger.
        /// </summary>
        /// <param name="messenger">The messenger instance to use.</param>
        protected ValidatableViewModelBase(IMessenger messenger) : base()
        {
            _messenger = messenger;
        }

        /// <summary>
        /// Initializes a new instance with both a messenger and validation context.
        /// </summary>
        /// <param name="messenger">The messenger instance to use.</param>
        /// <param name="validationContext">The validation context to use.</param>
        protected ValidatableViewModelBase(IMessenger messenger, ValidationContext validationContext)
            : base(validationContext)
        {
            _messenger = messenger;
        }

        /// <summary>
        /// Called when the recipient is activated. Override to register message handlers.
        /// </summary>
        protected virtual void OnActivated()
        {
            Messenger.RegisterAll(this);
        }

        /// <summary>
        /// Called when the recipient is deactivated. Override to perform cleanup.
        /// </summary>
        protected virtual void OnDeactivated()
        {
            Messenger.UnregisterAll(this);
        }

        /// <summary>
        /// Default message receiver. Override in derived classes to handle specific message types.
        /// </summary>
        /// <param name="message">The received message.</param>
        void IRecipient<object>.Receive(object message)
        {
            // Default implementation does nothing.
            // Derived classes should register for specific message types.
        }

        /// <summary>
        /// Validates all properties and returns whether the model is valid.
        /// </summary>
        /// <returns><c>true</c> if no validation errors exist; otherwise, <c>false</c>.</returns>
        public bool ValidateAndCheckErrors()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        /// <summary>
        /// Gets all validation errors as a flat collection of error messages.
        /// </summary>
        /// <returns>A collection of all validation error messages.</returns>
        public IEnumerable<string> GetAllErrorMessages()
        {
            return GetErrors()
                .Cast<ValidationResult>()
                .Where(r => r != ValidationResult.Success)
                .SelectMany(r => r.ErrorMessage is not null ? new[] { r.ErrorMessage } : Array.Empty<string>());
        }

        /// <summary>
        /// Gets validation errors for a specific property as a list of strings.
        /// </summary>
        /// <param name="propertyName">The property name to get errors for.</param>
        /// <returns>A list of error messages for the property.</returns>
        public IReadOnlyList<string> GetPropertyErrorMessages(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return Array.Empty<string>();

            var errors = GetErrors(propertyName);
            if (errors is null)
                return Array.Empty<string>();

            return errors
                .Cast<ValidationResult>()
                .Where(r => r != ValidationResult.Success && r.ErrorMessage is not null)
                .Select(r => r.ErrorMessage!)
                .ToList();
        }

        /// <summary>
        /// Clears all validation errors. Useful when resetting form state.
        /// </summary>
        public void ClearAllErrors()
        {
            ClearErrors();
        }

        /// <summary>
        /// Helper method to set a property value and optionally validate it.
        /// Use this for properties that need explicit validation control.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="field">Reference to the backing field.</param>
        /// <param name="newValue">The new value to set.</param>
        /// <param name="validate">Whether to validate the property after setting.</param>
        /// <param name="propertyName">The property name (auto-filled by compiler).</param>
        /// <returns><c>true</c> if the property was changed; otherwise, <c>false</c>.</returns>
        protected bool SetPropertyWithValidation<T>(
            ref T field,
            T newValue,
            bool validate = true,
            [CallerMemberName] string? propertyName = null)
        {
            return SetProperty(ref field, newValue, validate, propertyName!);
        }

        /// <summary>
        /// Validates a dependent property when the source property changes.
        /// Use this in OnPropertyChanged handlers for cross-property validation.
        /// </summary>
        /// <param name="dependentPropertyName">The name of the dependent property to validate.</param>
        protected void ValidateDependentProperty(string dependentPropertyName)
        {
            ValidateProperty(
                GetType().GetProperty(dependentPropertyName)?.GetValue(this),
                dependentPropertyName);
        }
    }
}
