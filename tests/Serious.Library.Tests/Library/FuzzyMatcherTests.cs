using Serious.Text;
using Xunit;

public class FuzzyMatcherTests
{
    public class TheFuzzyMatchMethod
    {
        [Theory]
        [InlineData("address", "address")]
        [InlineData("graph", "graph")]
        [InlineData("phil's address", "address")]
        [InlineData("phil's address", "addr")]
        [InlineData("phil's address", "phil")]
        [InlineData("phil's address", "phil address")]
        [InlineData("peanut butter cookie", "panut butter cookie")]
        [InlineData("Test your graphs", "graphs")]
        public void ReturnsTrueForSimilarMatches(string stringToSearch, string pattern)
        {
            Assert.True(stringToSearch.FuzzyMatch(pattern));
        }

        [Theory]
        [InlineData("An example integration with Wolfram Alpha's Short Answers API.", "graph")]
        [InlineData("Remember things. Associate text and urls to a keyword or phrase.", "graph")]
        [InlineData("a troublemaker", "a peanut")]
        [InlineData("abbot is antagonistic", "address")]
        [InlineData("abbot", "address")]
        [InlineData("is antagonistic", "address")]
        [InlineData("asdf", "aCommandThatDoesNotExist")]
        [InlineData("Another rando skill.", "aCommandThatDoesNotExist")]
        [InlineData("Another", "aCommandThatDoesNotExist")]
        [InlineData("rando", "aCommandThatDoesNotExist")]
        [InlineData("skill", "aCommandThatDoesNotExist")]
        public void ReturnsFalseForNonMatches(string stringToSearch, string pattern)
        {
            Assert.False(stringToSearch.FuzzyMatch(pattern));
        }
    }

    public class TheNormalizePatternMethod
    {
        [Theory]
        [InlineData("another test", "another test")]
        [InlineData("a_test", "a_test")]
        [InlineData("another test.", "another test")]
        [InlineData("another!! **test?**", "another test")]
        public void StripsPunctuationEtc(string source, string expected)
        {
            var result = FuzzyMatcher.NormalizePattern(source);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("a test", "test")]
        [InlineData("Mr. test", "test")]
        [InlineData("Mrs. test", "test")]
        [InlineData("The test of ages", "test ages")]
        [InlineData("The great test of ages now", "great test ages now")]
        [InlineData("an excellent test", "excellent test")]
        [InlineData("i like the test", "like test")]
        [InlineData("abbot is antagonistic", "abbot antagonistic")]
        public void StripsPrepositionsAndTheLike(string source, string expected)
        {
            var result = FuzzyMatcher.NormalizePattern(source);

            Assert.Equal(expected, result);
        }
    }
}
