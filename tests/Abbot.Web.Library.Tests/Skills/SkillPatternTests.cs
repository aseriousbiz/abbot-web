using System.Linq;
using Serious.Abbot.Skills;
using Xunit;

public class SkillPatternsTests
{
    public class TheMatchRememberPatternMethod
    {
        [Fact]
        public void ReturnsEmptyStringForKeyIfNoKeyPresent()
        {
            var (key, value) = SkillPatterns.MatchRememberPattern("");

            Assert.Equal(string.Empty, key);
            Assert.Equal(string.Empty, value);
        }

        [Theory]
        [InlineData("paulnakata is https://twitter.com/paulnakata?lang=en", "paulnakata", "https://twitter.com/paulnakata?lang=en")]
        [InlineData("haiku is 古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと", "haiku", "古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと")]
        [InlineData("phil haack is a man who is devious", "phil haack", "a man who is devious")]
        [InlineData("\"phil haack is a man who\" is devious", "phil haack is a man who", "devious")]
        [InlineData("'phil haack is a man who' is devious", "phil haack is a man who", "devious")]
        public void SplitsAlongTheIsKeyword(string text, string expectedKey, string expectedValue)
        {
            var (key, value) = SkillPatterns.MatchRememberPattern(text);

            Assert.Equal(expectedKey, key);
            Assert.Equal(expectedValue, value);
        }

        [Theory]
        [InlineData("paulnakata = https://twitter.com/paulnakata?lang=en", "paulnakata", "https://twitter.com/paulnakata?lang=en")]
        [InlineData("haiku = 古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと", "haiku", "古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと")]
        [InlineData("phil haack = a man who = devious", "phil haack", "a man who = devious")]
        [InlineData("\"phil haack = a man who\" = devious", "phil haack = a man who", "devious")]
        [InlineData("'phil haack = a man who' = devious", "phil haack = a man who", "devious")]
        public void SplitsAlongTheIsEqualSign(string text, string expectedKey, string expectedValue)
        {
            var (key, value) = SkillPatterns.MatchRememberPattern(text);

            Assert.Equal(expectedKey, key);
            Assert.Equal(expectedValue, value);
        }

        [Theory]
        [InlineData("foo to bar", "foo", "bar")]
        [InlineData("haiku to 古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと", "haiku", "古池や蛙飛び込む水の音\nふるいけやかわずとびこむみずのおと")]
        [InlineData("phil haack to a man who to devious", "phil haack", "a man who to devious")]
        [InlineData("\"phil haack to a man who\" to devious", "phil haack to a man who", "devious")]
        [InlineData("'phil haack to a man who' to devious", "phil haack to a man who", "devious")]
        public void SplitsAlongTheToKeyword(string text, string expectedKey, string expectedValue)
        {
            var (key, value) = SkillPatterns.MatchRememberPattern(text);

            Assert.Equal(expectedKey, key);
            Assert.Equal(expectedValue, value);
        }

        [Theory]
        [InlineData("paulnakata", "paulnakata")]
        [InlineData("\"phil haack is a man who\"", "phil haack is a man who")]
        [InlineData("\"phil haack “is” a man who\"", "phil haack “is” a man who")]
        [InlineData("'phil haack is a \"man\" who'", "phil haack is a \"man\" who")]
        public void ParsesKeyOutOfStatement(string text, string expectedKey)
        {
            var (key, value) = SkillPatterns.MatchRememberPattern(text);

            Assert.Equal(expectedKey, key);
            Assert.Equal("", value);
        }

        [Theory]
        [InlineData("|address me")]
        [InlineData("| address me")]
        public void ParsesSearchCommand(string args)
        {
            var (key, value) = SkillPatterns.MatchRememberPattern(args);

            Assert.Equal("|", key);
            Assert.Equal("address me", value);
        }
    }

    public class TheMatchEchoPatternMethod
    {
        [Theory]
        [InlineData("", false, "", "", "", "")]
        [InlineData("some text", false, "some text", "", "", "")]
        [InlineData("format:someformat", false, "", "someformat", "", "")]
        [InlineData("format:someformat  some text", false, "some text", "someformat", "", "")]
        [InlineData("user:U1234567 format:someformat   some text", false, "some text", "someformat", "U1234567", "")]
        [InlineData("room:C012345    some text", false, "some text", "", "", "C012345")]
        [InlineData("!thread", true, "", "", "", "")]
        [InlineData("!thread some text", true, "some text", "", "", "")]
        [InlineData("!thread format:someformat some text", true, "some text", "someformat", "", "")]
        public void MatchesEchoPatterns(
            string text,
            bool expectedThread,
            string expectedEchoText,
            string expectedFormat,
            string expectedUser,
            string expectedRoom)
        {
            var (thread, echoText, format, user, room) = SkillPatterns.MatchEchoPattern(text);
            Assert.Equal(expectedThread, thread);
            Assert.Equal(expectedEchoText, echoText);
            Assert.Equal(expectedFormat, format);
            Assert.Equal(user, expectedUser);
            Assert.Equal(room, expectedRoom);
        }
    }

    public class TheMatchMentionsMethod
    {
        [Theory]
        [InlineData("<@U012LKJFG0P> <@U02AG6QJP>")]
        [InlineData("a <@U012LKJFG0P> and a <@U02AG6QJP> work hard")]
        [InlineData("foo@bara <@U012LKJFG0P> and a\n<@U02AG6QJP>\n")]
        public void MatchesAbbotNormalFormMentions(string args)
        {
            var mentions = SkillPatterns.ParseMentions(args).ToList();
            Assert.Equal(2, mentions.Count);
            Assert.Equal("U012LKJFG0P", mentions[0]);
            Assert.Equal("U02AG6QJP", mentions[1]);
        }
    }
}
