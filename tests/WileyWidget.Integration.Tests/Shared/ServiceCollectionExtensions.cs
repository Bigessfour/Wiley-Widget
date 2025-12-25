using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;
using WileyWidget.Business.Interfaces;
using WileyWidget.Business.Services;

namespace WileyWidget.Integration.Tests.Shared
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Replace the production IGrokRecommendationService with an instance that uses the provided MockHttpMessageHandler.
        /// Useful for integration tests that want to verify Grok parsing logic without calling the real API.
        /// </summary>
        public static IServiceCollection ReplaceGrokWithMockHttp(this IServiceCollection services, MockHttpMessageHandler handler)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            // Remove existing IGrokRecommendationService registrations
            var existing = services.Where(sd => sd.ServiceType == typeof(IGrokRecommendationService)).ToList();
            foreach (var sd in existing)
            {
                services.Remove(sd);
            }

            services.AddScoped<IGrokRecommendationService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<GrokRecommendationService>>();
                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://api.x.ai/")
                };

                return new GrokRecommendationService(logger, client);
            });

            return services;
        }
    }
}
