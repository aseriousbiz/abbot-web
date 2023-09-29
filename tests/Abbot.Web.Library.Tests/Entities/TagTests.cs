using Serious.Abbot.Entities;

public class TagTests
{
    public class TheIsValidTagNameMethod
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("tag", true)]
        [InlineData("system:tag", false)]
        [InlineData("a-legendary-tag", true)]
        [InlineData("a \"legendary/tag", false)]
        [InlineData("sentiment:negative", false)]
        [InlineData("a `legendary/tag", false)]
        [InlineData("a _*legendary*_/tag", false)]
        public void AllowsMrkdwnSafeCharacters(string tagName, bool expected)
        {
            var result = Tag.IsValidTagName(tagName);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("tag", true)]
        [InlineData("system:tag", true)]
        [InlineData("system:tag:foo", false)]
        [InlineData("a-legendary-tag", true)]
        [InlineData("a \"legendary/tag", false)]
        [InlineData("sentiment:negative", true)]
        [InlineData("a `legendary/tag", false)]
        [InlineData("a _*legendary*_/tag", false)]
        public void AllowsGeneratedTagNamesWhenSpecified(string tagName, bool expected)
        {
            var result = Tag.IsValidTagName(tagName, allowGenerated: true);
            Assert.Equal(expected, result);
        }
    }
}
