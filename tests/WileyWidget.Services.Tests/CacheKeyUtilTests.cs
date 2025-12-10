using FluentAssertions;
using Xunit;
using WileyWidget.Services.Helpers;

namespace WileyWidget.Tests
{
    public class CacheKeyUtilTests
    {
        [Fact]
        public void Generate_SameInputs_ReturnsSameKey()
        {
            var k1 = CacheKeyUtil.Generate("XAI", "v1", "hello", "world");
            var k2 = CacheKeyUtil.Generate("XAI", "v1", "hello", "world");

            k1.Should().Be(k2);
        }

        [Fact]
        public void Generate_WhitespaceAndCaseNormalized_ReturnsSameKey()
        {
            var k1 = CacheKeyUtil.Generate("XAI", "v1", "Hello   World");
            var k2 = CacheKeyUtil.Generate("XAI", "v1", "hello world");

            k1.Should().Be(k2);
        }

        [Fact]
        public void Generate_DifferentVersion_ReturnsDifferentKey()
        {
            var k1 = CacheKeyUtil.Generate("XAI", "v1", "my_prompt");
            var k2 = CacheKeyUtil.Generate("XAI", "v2", "my_prompt");

            k1.Should().NotBe(k2);
        }

        [Fact]
        public void Generate_NullInput_TreatedAsEmpty()
        {
            var k1 = CacheKeyUtil.Generate("XAI", "v1", null, "a");
            var k2 = CacheKeyUtil.Generate("XAI", "v1", string.Empty, "a");

            k1.Should().Be(k2);
        }

        [Fact]
        public void Generate_OrderMatters_ReturnsDifferentKeys()
        {
            var k1 = CacheKeyUtil.Generate("XAI", "v1", "one", "two");
            var k2 = CacheKeyUtil.Generate("XAI", "v1", "two", "one");

            k1.Should().NotBe(k2);
        }

        [Fact]
        public void Generate_HashIsSha256Hex()
        {
            var key = CacheKeyUtil.Generate("XAI", "v1", "test");
            var parts = key.Split(':');
            parts.Length.Should().Be(3);
            parts[0].Should().Be("XAI");
            parts[1].Should().Be("v1");
            parts[2].Length.Should().Be(64); // sha256 hex length
        }
    }
}
