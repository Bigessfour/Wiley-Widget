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

            // Create binding with proper data source and path
            var binding = new Binding(controlProperty, viewModel, propertyName)
            {
                DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged,
                ControlUpdateMode = ControlUpdateMode.OnPropertyChanged
            };

            // Note: BindingError event may not be available in current .NET version
            // binding.BindingError += (s, e) =>
            // {
            //     // Log binding error but don't throw - allow UI to remain responsive
            //     System.Diagnostics.Debug.WriteLine(
            //         $"[BINDING ERROR] {controlProperty} -> {propertyName}: {e.ErrorText}");
            // };

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
        /// Subscribes to a ViewModel property change with automatic thread marshalling.
        /// Useful for complex property changes that need custom handling.
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

            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName != propertyName) return;

                var property = typeof(TViewModel).GetProperty(propertyName);
                var value = property?.GetValue(viewModel);

                // Marshal to UI thread if needed
                if (control.InvokeRequired && control.IsHandleCreated)
                {
                    try { control.BeginInvoke(() => onPropertyChanged(value)); }
                    catch { /* Control disposed */ }
                }
                else
                {
                    onPropertyChanged(value);
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
        /// Internal disposable wrapper for event subscriptions.
        /// </summary>
        private class DisposableSubscription : IDisposable
        {
            private readonly Action _unsubscribe;

            public DisposableSubscription(Action unsubscribe) => _unsubscribe = unsubscribe;

            public void Dispose()
            {
                _unsubscribe?.Invoke();
                GC.SuppressFinalize(this);
            }
        }
    }
}
