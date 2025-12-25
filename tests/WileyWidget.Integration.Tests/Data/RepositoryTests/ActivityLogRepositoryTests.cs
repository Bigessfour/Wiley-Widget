using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Business.Interfaces;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Integration.Tests.Shared;
using Xunit;

namespace WileyWidget.Integration.Tests.Data.RepositoryTests
{
    public class ActivityLogRepositoryTests : IntegrationTestBase
    {
        public ActivityLogRepositoryTests() : base()
        {
        }

        [Fact]
        public async Task LogActivityAsync_PersistsAnd_GetRecentActivities_ReturnsIt()
        {
            // Arrange
            var repo = GetRequiredService<IActivityLogRepository>();

            // Ensure clean DB
            await ResetDatabaseAsync();

            var log = new ActivityLog
            {
                Activity = "IntegrationTestActivity",
                Details = "Some details",
                User = "test-user",
                Category = "integration",
                ActivityType = "test"
            };

            // Act
            await repo.LogActivityAsync(log);

            var items = await repo.GetRecentActivitiesAsync(0, 10);

            // Assert
            items.Should().NotBeNull();
            items.Should().ContainSingle(i => i.Activity == "IntegrationTestActivity" && i.User == "test-user");
        }
    }
}