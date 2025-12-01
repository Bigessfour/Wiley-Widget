using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WileyWidget.WinForms.ViewModels
{
    /// <summary>
    /// Small concrete adapter so code can instantiate a concrete validator at runtime.
    /// CommunityToolkit.Mvvm.ObservableValidator is intentionally a base (abstract) type
    /// in this package — create a trivial concrete subclass to instantiate in generated
    /// validation helper files and work with the Toolkit APIs.
    /// </summary>
    internal sealed class WinFormsObservableValidator : ObservableValidator
    {
        public WinFormsObservableValidator() : base()
        {
        }

        public WinFormsObservableValidator(ValidationContext validationContext) : base(validationContext)
        {
        }

        public WinFormsObservableValidator(IDictionary<object, object?>? items) : base(items)
        {
        }

        public WinFormsObservableValidator(IDictionary<string, object>? items) : base(items is null ? null : new Dictionary<object, object?>(items.ToDictionary(kv => (object)kv.Key, kv => (object?)kv.Value)))
        {
        }
    }
}
