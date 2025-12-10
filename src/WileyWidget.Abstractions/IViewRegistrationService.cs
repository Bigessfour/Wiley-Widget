using System;
using System.Collections.Generic;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Service for managing Prism region view registrations
    /// </summary>
    public interface IViewRegistrationService
    {
        [Obsolete("RegisterAllViews is deprecated. Register views in Prism modules instead.")]
        void RegisterAllViews();

        bool RegisterView(string regionName, Type viewType);
        bool IsViewRegistered(string viewName);
        IEnumerable<Type> GetRegisteredViews(string regionName);
        RegionValidationResult ValidateRegions();
    }

    /// <summary>
    /// Result of region validation operation
    /// </summary>
    public class RegionValidationResult
    {
        public bool IsValid { get; set; }
        public int TotalRegions { get; set; }
        public int ValidRegionsCount { get; set; }
        public List<string> ValidRegions { get; set; } = new List<string>();
        public List<string> MissingRegions { get; set; } = new List<string>();
        public Dictionary<string, int> RegionViewCounts { get; set; } = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"RegionValidation: {ValidRegionsCount}/{TotalRegions} valid, IsValid: {IsValid}";
        }
    }
}
