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

        [Fact]
        public void Parse_PreservesFirstOccurrenceOrderAcrossCaseInsensitiveDuplicates()
        {
            var parser = new LocalListParser();

            var result = parser.Parse("  Chribba  \nAURA\nAura\nThe Mittani\nchribba");

            Assert.Equal(new[] { "Chribba", "AURA", "The Mittani" }, result);
        }

        [Fact]
        public void Parse_ReturnsEmptyListForWhitespaceOnlyInput()
        {
            var parser = new LocalListParser();

            var result = parser.Parse(" \r\n\t \n");

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_RejectsNamesThatContainInvalidSeparators()
        {
            var parser = new LocalListParser();

            var result = parser.Parse("Valid Name\nBad//Name\nAlso..Bad\nAnother Valid");

            Assert.Equal(new[] { "Valid Name", "Another Valid" }, result);
        }
    }
}
