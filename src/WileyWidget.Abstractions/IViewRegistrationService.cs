using System;
using System.Collections.Generic;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Service for managing region view registrations
    /// </summary>
    /// <summary>
    /// Represents a interface for iviewregistrationservice.
    /// </summary>
    public interface IViewRegistrationService
    {
        [Obsolete("RegisterAllViews is deprecated. Register views in modules instead.")]
        void RegisterAllViews();

        bool RegisterView(string regionName, Type viewType);
        bool IsViewRegistered(string viewName);
        IEnumerable<Type> GetRegisteredViews(string regionName);
        RegionValidationResult ValidateRegions();
    }

    /// <summary>
    /// Result of region validation operation
    /// </summary>
    /// <summary>
    /// Represents a class for regionvalidationresult.
    /// </summary>
    public class RegionValidationResult
    {
        /// <summary>
        /// Gets or sets the isvalid.
        /// </summary>
        public bool IsValid { get; set; }
        /// <summary>
        /// Gets or sets the totalregions.
        /// </summary>
        public int TotalRegions { get; set; }
        /// <summary>
        /// Gets or sets the validregionscount.
        /// </summary>
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
