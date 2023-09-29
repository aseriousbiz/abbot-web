using Serious;
using Xunit;

public class StringExtensionsTests
{
    public class TheLeftBeforeMethod
    {
        [Fact]
        public void ReturnsLeftBeforeLastOccurrenceOfSuffix()
        {
            Assert.Equal("Skill", "SkillSkill".LeftBefore("Skill", StringComparison.Ordinal));
        }

        [Fact]
        public void ReturnsWholeWordWhenSuffixDoesNotExist()
        {
            Assert.Equal("McLovin", "McLovin".LeftBefore("Skill", StringComparison.Ordinal));
        }
    }

    public class TheTrimSuffixMethod
    {
        [Theory]
        [InlineData("", "", "")]
        [InlineData("", "Suffix", "")]
        [InlineData("TheSuffix", "", "TheSuffix")]
        [InlineData("TheSuffix", "Suffix", "The")]
        [InlineData("StringTheSkill", "Skill", "StringThe")]
        [InlineData("Something", "Suffix", "Something")]
        public void StripsSuffixesTheWayWeExpect(string text, string suffix, string expected)
        {
            var result = text.TrimSuffix(suffix, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(expected, result);
        }
    }

    public class TheTrimLeadingWhitespaceMethod
    {
        [Theory]
        [InlineData("", "", 0)]
        [InlineData("Hello World", "Hello World", 0)]
        [InlineData(" Hello World", "Hello World", 1)]
        [InlineData("    Hello World", "Hello World", 4)]
        [InlineData("  Trim   ", "Trim   ", 2)]
        [InlineData("\t\tTrim\t\t\t", "Trim\t\t\t", 2)]
        public void ReturnsExpectedResult(string input, string expectedText, int expectedTrimmedCount)
        {
            TrimResult result = input.TrimLeadingWhitespace();

            Assert.Equal(expectedText, result.Text);
            Assert.Equal(expectedTrimmedCount, result.TrimmedCount);
        }
    }

    public class TheTrimTrailingWhitespaceMethod
    {
        [Theory]
        [InlineData("", "", 0)]
        [InlineData("Hello World", "Hello World", 0)]
        [InlineData("Hello World ", "Hello World", 1)]
        [InlineData("Hello World    ", "Hello World", 4)]
        [InlineData("  Trim   ", "  Trim", 3)]
        [InlineData("\t\tTrim\t\t\t", "\t\tTrim", 3)]
        public void ReturnsExpectedResult(string input, string expectedText, int expectedTrimmedCount)
        {
            TrimResult result = input.TrimTrailingWhitespace();

            Assert.Equal(expectedText, result.Text);
            Assert.Equal(expectedTrimmedCount, result.TrimmedCount);
        }
    }

    public class ThePrefixWithIndefiniteArticleMethod
    {
        [Theory]
        [InlineData("car", "A car")]
        [InlineData("car park", "A car park")]
        [InlineData("apple", "An apple")]
        [InlineData("hat", "A hat")]
        [InlineData("hourglass", "An hourglass")]
        [InlineData("honorary degree", "An honorary degree")]
        [InlineData("unique perspective", "A unique perspective")]
        [InlineData("one-eyed pirate", "A one-eyed pirate")]
        public void PrefixesWithCorrectIndefiniteArticle(string sentence, string expected)
        {
            var result = sentence.PrefixWithIndefiniteArticle();
            Assert.Equal(expected, result);
        }
    }

    public class TheEnsureEndsWithPunctuationMethod
    {
        [Theory]
        [InlineData("car", "car.")]
        [InlineData("car park.", "car park.")]
        [InlineData("apple!", "apple!")]
        [InlineData("I said, \"I love my hat\"", "I said, \"I love my hat\"")]
        [InlineData("I said, \"I love my hat!\"", "I said, \"I love my hat!\"")]
        [InlineData("hourglass?", "hourglass?")]
        [InlineData("", "")]
        public void EnsuresSentenceEndsWithPunctuation(string sentence, string expected)
        {
            var result = sentence.EnsureEndsWithPunctuation();
            Assert.Equal(expected, result);
        }
    }

    public class TheCapitalizeMethod
    {
        [Theory]
        [InlineData("TITLE", "TITLE")]
        [InlineData("title", "Title")]
        [InlineData("t", "T")]
        [InlineData("", "")]
        [InlineData("tItle", "TItle")]
        [InlineData("Title", "Title")]
        [InlineData("an HTTP trigger", "An HTTP trigger")]
        public void CapitalizesFirstLetter(string value, string expected)
        {
            Assert.Equal(expected, value.Capitalize());
        }
    }

    public class TheToPascalCaseMethod
    {
        [Theory]
        [InlineData("TITLE of my CoolMovie", "TitleOfMyCoolMovie")]
        [InlineData("title", "Title")]
        [InlineData("title of the song", "TitleOfTheSong")]
        public void CombinesWordInPascalCase(string value, string expected)
        {
            Assert.Equal(expected, value.ToPascalCase());
        }
    }

    public class TheToCamelCaseMethod
    {
        [Theory]
        [InlineData("TITLE of my CoolMovie", "titleOfMyCoolMovie")]
        [InlineData("title", "title")]
        [InlineData("title of the song", "titleOfTheSong")]
        public void CombinesWordInCamelCase(string value, string expected)
        {
            Assert.Equal(expected, value.ToCamelCase());
        }
    }

    public class TheToSnakeCaseMethod
    {
        [Theory]
        [InlineData("TITLE of my CoolMovie", "title_of_my_cool_movie")]
        [InlineData("title", "title")]
        [InlineData("title of the song", "title_of_the_song")]
        public void CombinesWordInSnakeCase(string value, string expected)
        {
            Assert.Equal(expected, value.ToSnakeCase());
        }
    }

    public class TheToDashCaseMethod
    {
        [Theory]
        [InlineData("TITLE of my CoolMovie", "tITLE-of-my-cool-movie")]
        [InlineData("title", "title")]
        [InlineData("title of the song", "title-of-the-song")]
        public void CombinesWordInDashCase(string value, string expected)
        {
            Assert.Equal(expected, value.ToDashCase());
        }
    }

    public class TheToSlugMethod
    {
        [Theory]
        [InlineData("TITLE of my CoolMovie", "title-of-my-coolmovie")]
        [InlineData("title", "title")]
        [InlineData("title 123", "title-123")]
        [InlineData("title of the song", "title-of-the-song")]
        [InlineData("title  of  the song?  ", "title-of-the-song")]
        [InlineData("-title  of  the song#-", "title-of-the-song")]
        [InlineData("title--of---the song!-", "title-of-the-song")]
        [InlineData("äbboť  iš  cõöl", "abbot-is-cool")]
        [InlineData("АВГД ЄЅЗИѲ", "авгд-єѕзиѳ")]
        [InlineData("안녕 친구들", "안녕-친구들")]
        public void CombinesWordInLowerDashCase(string value, string expected)
        {
            Assert.Equal(expected, value.ToSlug());
        }
    }

    public class TheTruncateAtNextWhitespaceMethod
    {
        [Theory]
        [InlineData(1, "This is a test.", "This…")]
        [InlineData(4, "This is a test.", "This…")]
        [InlineData(5, "This is a test.", "This…")]
        [InlineData(6, "This is a test.", "This is…")]
        [InlineData(14, "This is a test.", "This is a test.")]
        [InlineData(15, "This is a test.", "This is a test.")]
        [InlineData(21, "This is a test.", "This is a test.")]
        public void OnlyTruncatesAtWordBoundary(int length, string text, string expected)
        {
            Assert.Equal(expected, text.TruncateAtWordBoundary(length, true));
        }
    }

    public class TheFindLongestCommonPrefixMethod
    {
        [Theory]
        [InlineData(0.2, "Haacked Dev Hoo")]
        [InlineData(0.8, "Haacked Dev ")]
        [InlineData(0.9, "Haacked ")]
        public void ReturnsLongestPrefix(double threshold, string expected)
        {
            var names = new[]
            {
                "Haacked Dev Bluth Company",
                "Haacked Dev Cyberdyne",
                "Haacked Dev Dunder Mifflin",
                "Haacked Dev Hooli",
                "Haacked Dev Hoops",
                "Haacked Dev Initech",
                "Haacked Test Hooli"
            };

            var result = names.FindLongestCommonPrefix(threshold);

            Assert.Equal(expected, result);
        }
    }

}
