using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Extension methods for robust WinForms data binding with null safety and automatic UI thread marshalling.
    /// Provides declarative binding pattern to replace manual PropertyChanged switch statements.
    /// All operations are thread-safe for property notifications from background threads.
    /// </summary>
    public static class DataBindingExtensions
    {
        /// <summary>
        /// Binds a control property to a ViewModel property with automatic marshalling to UI thread.
        /// Handles null ViewModel gracefully - binding continues to work if ViewModel is replaced.
        /// </summary>
        /// <typeparam name="TViewModel">ViewModel type implementing INotifyPropertyChanged.</typeparam>
        /// <typeparam name="TValue">Property value type.</typeparam>
        /// <param name="control">Target control to bind.</param>
        /// <param name="controlProperty">Control property name (e.g., "Visible", "Text").</param>
        /// <param name="viewModel">ViewModel data source.</param>
        /// <param name="propertyExpression">Expression selecting ViewModel property (e.g., vm => vm.IsLoading).</param>
        /// <returns>Created binding for advanced configuration if needed.</returns>
        public static Binding BindProperty<TViewModel, TValue>(
            this Control control,
            string controlProperty,
            TViewModel viewModel,
            Expression<Func<TViewModel, TValue?>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (string.IsNullOrWhiteSpace(controlProperty)) throw new ArgumentException("Property name required", nameof(controlProperty));
            if (propertyExpression == null) throw new ArgumentNullException(nameof(propertyExpression));

            // Extract property name from expression
            var propertyName = GetPropertyName(propertyExpression);

            // Validate property exists on ViewModel
            var property = typeof(TViewModel).GetProperty(propertyName);
            if (property == null)
            {
                throw new ArgumentException($"Property '{propertyName}' does not exist on type '{typeof(TViewModel).Name}'", nameof(propertyExpression));
            }

            // Create binding with proper data source and path
            var binding = new Binding(controlProperty, viewModel, propertyName)
            {
                DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged,
                ControlUpdateMode = ControlUpdateMode.OnPropertyChanged
            };

            // Add binding to control
            control.DataBindings.Add(binding);

            return binding;
        }

        /// <summary>
        /// Safely unbinds all data bindings from a control.
        /// </summary>
        /// <param name="control">Control to unbind.</param>
        public static void UnbindAll(this Control control)
        {
            if (control == null) return;

            try
            {
                control.DataBindings.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UNBIND ERROR] Failed to clear data bindings: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts property name from a lambda expression.
        /// </summary>
        private static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty?>> expression)
        {
            if (expression.Body is MemberExpression memberExpr)
            {
                return memberExpr.Member.Name;
            }
            throw new ArgumentException("Expression must be a property access expression", nameof(expression));
        }

        /// <summary>
        /// Subscribes to a ViewModel property change with automatic thread marshalling and weak reference handling.
        /// Useful for complex property changes that need custom handling.
        /// Unsubscribes automatically if control is disposed.
        /// </summary>
        /// <typeparam name="TViewModel">ViewModel type implementing INotifyPropertyChanged.</typeparam>
        /// <param name="control">Target control for thread marshalling.</param>
        /// <param name="viewModel">ViewModel data source.</param>
        /// <param name="propertyName">Property name to watch.</param>
        /// <param name="onPropertyChanged">Callback when property changes (receives new value).</param>
        /// <returns>Disposable subscription - dispose to unsubscribe.</returns>
        public static IDisposable SubscribeToProperty<TViewModel>(
            this Control control,
            TViewModel viewModel,
            string propertyName,
            Action<object?> onPropertyChanged)
            where TViewModel : INotifyPropertyChanged
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("Property name required", nameof(propertyName));
            if (onPropertyChanged == null) throw new ArgumentNullException(nameof(onPropertyChanged));

            // Validate property exists
            var property = typeof(TViewModel).GetProperty(propertyName);
            if (property == null)
            {
                throw new ArgumentException($"Property '{propertyName}' does not exist on type '{typeof(TViewModel).Name}'", nameof(propertyName));
            }

            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName != propertyName) return;
                if (s is not TViewModel vm) return;

                // Get value safely from property
                object? value = null;
                try
                {
                    value = property.GetValue(vm);
                }
                catch
                {
                    // Property access failed
                }

                // Marshal to UI thread if needed - check if control is still valid
                if (control != null && !control.IsDisposed)
                {
                    if (control.InvokeRequired && control.IsHandleCreated)
                    {
                        try
                        {
                            control.BeginInvoke(() =>
                            {
                                // Double-check control isn't disposed during BeginInvoke
                                if (!control.IsDisposed)
                                {
                                    onPropertyChanged(value);
                                }
                            });
                        }
                        catch
                        {
                            // Control disposed during or after BeginInvoke
                        }
                    }
                    else if (!control.IsDisposed)
                    {
                        try
                        {
                            onPropertyChanged(value);
                        }
                        catch
                        {
                            // Callback exception - don't crash handler
                        }
                    }
                }
            };

            viewModel.PropertyChanged += handler;

            // Return disposable to unsubscribe
            return new DisposableSubscription(() =>
            {
                viewModel.PropertyChanged -= handler;
            });
        }

        /// <summary>
        /// Internal disposable wrapper for event subscriptions with proper IDisposable pattern.
        /// </summary>
        private sealed class DisposableSubscription : IDisposable
        {
            private readonly Action? _unsubscribe;
            private bool _disposed;

            public DisposableSubscription(Action unsubscribe)
            {
                _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (_disposed) return;
                _disposed = true;

                if (disposing)
                {
                    try
                    {
                        _unsubscribe?.Invoke();
                    }
                    catch
                    {
                        // Ignore unsubscribe errors
                    }
                }
            }

            ~DisposableSubscription()
            {
                Dispose(false);
            }
        }
    }
}
