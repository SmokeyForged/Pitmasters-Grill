using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class LocalListParserTests
    {
        [Fact]
        public void Parse_FiltersNoiseAndDeduplicatesNames()
        {
            var parser = new LocalListParser();

            var result = parser.Parse("Aura\n \nAURA\nnot/a/pilot\nChribba");

            Assert.Equal(new[] { "Aura", "Chribba" }, result);
        }
    }
}
