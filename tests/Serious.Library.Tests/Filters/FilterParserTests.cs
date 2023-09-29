using Serious.Filters;
using Xunit;

public class FilterParserTests
{
    public class TheParseMethod
    {
        [Fact]
        public static void CanParseFilterText()
        {
            var result = FilterParser.Parse(" tizzy tazzy tag:\"foo bar\" -tag:bye is:fun  busy buzy ");

            Assert.Equal(new[]
            {
                new Filter("tizzy tazzy"),
                Filter.Create("tag", "foo bar", "tag:\"foo bar\""),
                Filter.Create("-tag", "bye", "-tag:bye"),
                Filter.Create("is", "fun", "is:fun"),
                new Filter("busy buzy"),
            }, result.ToArray());
        }

        [Theory]
        [InlineData("we have a dangling delimiter:")]
        [InlineData("this is a tag: with no value")]
        [InlineData("dangling:")]
        public static void CanParseCasesWithNoFields(string text)
        {
            var result = FilterParser.Parse(text);

            Assert.Equal(new[]
            {
                new Filter(text),
            }, result.ToArray());
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("      ")]
        public static void CanParseEmptyText(string text)
        {
            var result = FilterParser.Parse(text);

            Assert.Empty(result);
        }
    }

    public class TheToStringMethod
    {
        [Fact]
        public void ReturnsFilterAsString()
        {
            var filter = new FilterList
            {
                Filter.Create("tag", "foo")
            };

            Assert.Equal("tag:foo", filter.ToString());
        }

        [Fact]
        public void ReturnsOriginalValue()
        {
            var filter = new FilterList {
                Filter.Create("tag", "foo", "tag:\"foo\"")
            };

            Assert.Equal("tag:\"foo\"", filter.ToString());
        }

        [Theory]
        [InlineData("foo bar")]
        [InlineData("\"foo bar\"")]
        public void ReturnsConstructedOriginalValues(string value)
        {
            var filter = new FilterList {
                Filter.Create("tag", value)
            };

            Assert.Equal("tag:\"foo bar\"", filter.ToString());
        }

        [Fact]
        public void ReturnsOriginalValues()
        {
            var filter = new FilterList {
                Filter.Create("tag", "foo"),
                new("is a "),
                Filter.Create("is", "fun"),
                Filter.Create("is", "bar")
            };

            Assert.Equal("tag:foo is a  is:fun is:bar", filter.ToString());
        }
    }
}
