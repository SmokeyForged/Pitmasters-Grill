using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class ZkillUrlBuilderTests
    {
        [Fact]
        public void BuildCharacterUrl_EncodesCharacterId()
        {
            var builder = new ZkillUrlBuilder();

            var result = builder.BuildCharacterUrl("123 45");

            Assert.Equal("https://zkillboard.com/character/123%2045/", result);
        }

        [Fact]
        public void BuildSearchUrl_ReturnsRootWhenNameIsBlank()
        {
            var builder = new ZkillUrlBuilder();

            var result = builder.BuildSearchUrl("  ");

            Assert.Equal("https://zkillboard.com/", result);
        }
    }
}
