using System;

namespace WileyWidget.Business.Configuration
{
    /// <summary>
    /// Options for the Grok recommendation service.
    /// </summary>
    public class GrokRecommendationOptions
    {
        /// <summary>
        /// Cache duration for recommendation results and explanations.
        /// Default: 2 hours.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(2);
    }
}
