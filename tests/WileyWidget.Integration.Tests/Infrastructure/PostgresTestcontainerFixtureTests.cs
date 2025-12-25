using System;
using Xunit;

namespace WileyWidget.Integration.Tests.Infrastructure
{
    public class PostgresTestcontainerFixtureTests
    {
        [Fact]
        public void GenerateSecureTestPassword_Returns_NonEmpty_And_ValidHex_And_IsRandom()
        {
            var p1 = PostgresTestcontainerFixture.GenerateSecureTestPassword();
            var p2 = PostgresTestcontainerFixture.GenerateSecureTestPassword();

            Assert.False(string.IsNullOrWhiteSpace(p1));
            Assert.True(p1.Length >= 64, "Expected at least 64 chars for 32 bytes hex representation.");
            Assert.NotEqual(p1, p2);

            // Hex uses uppercase by Convert.ToHexString, pattern 0-9A-F
            Assert.Matches("^[0-9A-F]+$", p1);
        }

        [Fact]
        public void GenerateSecureTestPassword_IsDifferentOnMultipleCalls()
        {
            var p1 = PostgresTestcontainerFixture.GenerateSecureTestPassword();
            var p2 = PostgresTestcontainerFixture.GenerateSecureTestPassword();
            Assert.NotEqual(p1, p2);
        }
    }
}
