using Serious.Text;
using Xunit;

public class MarkdownExtensionsTests
{

    public class TheToMarkdownInlineCodeMethod
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("foo", "`foo`")]
        [InlineData("`foo`", "`` `foo` ``")]
        [InlineData("``foo``", "`` `foo` ``")]
        [InlineData("````foo````", "`` ````foo```` ``")]
        public void DisallowsDoubleBackticks(string input, string expected)
        {
            var result = input.ToMarkdownInlineCode();
            Assert.Equal(expected, result);
        }
    }
}
