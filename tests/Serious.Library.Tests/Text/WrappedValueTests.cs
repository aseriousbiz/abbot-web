using Serious.Text;
using Xunit;

public class WrappedValueTests
{
    public class TheParseMethod
    {
        [Theory]
        [InlineData("", "", null)]
        [InlineData("foo|bar", "foo", "bar")]
        [InlineData("foo:bar|baz|qux", "foo:bar", "baz|qux")]
        [InlineData("foo|", "foo", "")]
        [InlineData("foo", "foo", null)]
        public void CanParseWrappedStrings(string value, string expectedExtraInformation, string? expectedOriginalValue)
        {
            var (extra, original) = WrappedValue.Parse(value);

            Assert.Equal(expectedExtraInformation, extra);
            Assert.Equal(expectedOriginalValue, original);
        }
    }

    public class TheToStringMethod
    {
        [Theory]
        [InlineData("", null, "")]
        [InlineData("foo:bar", null, "foo:bar")]
        [InlineData("foo:bar", "", "foo:bar|")]
        [InlineData("foo:bar", "baz", "foo:bar|baz")]
        [InlineData("foo:bar", "baz|qux", "foo:bar|baz|qux")]
        public void WritesDelimitedString(string extra, string? original, string expected)
        {
            var wrapped = new WrappedValue(extra, original);

            Assert.Equal(expected, wrapped.ToString());
        }
    }
}
