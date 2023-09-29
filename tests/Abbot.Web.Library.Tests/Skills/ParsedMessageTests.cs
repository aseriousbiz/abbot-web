using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Skills;

public class ParsedMessageTests
{
    public class TheParseMethod
    {
        [Theory]
        [InlineData("", "", false)]
        [InlineData(".", "", false)]
        [InlineData("message", "", false)]
        [InlineData("@U1234567", "", false)]
        [InlineData(".@U1234567", "@U1234567", false)]
        [InlineData("<at>U1234567</at>", "", false)]
        [InlineData("<@!U1234567>", "", false)]
        [InlineData("<@U1234567>", "", true)]
        [InlineData("hey <@U1234567>", "", true)]
        [InlineData("hey <@U1234567> how're you doing?", "how're you doing?", true)]
        [InlineData("hey   <@U1234567>    how're  you   doing?  ", "how're  you   doing?", true)]
        [InlineData(".    how're  you   doing?  ", "how're  you   doing?", false)]
        [InlineData("<@U99999> how about an @abbot cookie", "", false)]
        public void ParsesCommandToBot(string message, string expectedTextAfterBotMention, bool expectedIsCommand)
        {
            var result = ParsedMessage.Parse(message, "U1234567", '.');

            Assert.Equal(result.OriginalMessage, message);
            Assert.Equal(expectedTextAfterBotMention, result.CommandText);
            Assert.Equal(expectedIsCommand, result.IsBotCommand);
            Assert.Equal(string.Empty, result.PotentialSkillName);
            Assert.Equal(string.Empty, result.PotentialArguments);
        }

        [Theory]
        [InlineData("<@U9876Abbot> joke", "joke", "joke", "", "")]
        [InlineData("yo <@U9876Abbot> joke", "joke", "joke", "", "")]
        [InlineData("<@U9876Abbot> tell me a joke", "tell me a joke", "tell", "me a joke", "")]
        [InlineData("<@U9876Abbot> deepthought add A deep thought", "deepthought add A deep thought", "deepthought", "add A deep thought", "")]
        [InlineData("<@U9876Abbot> rem paul’s address is UNDISCLOSED LOCATION", "rem paul’s address is UNDISCLOSED LOCATION", "rem", "paul’s address is UNDISCLOSED LOCATION", "")]
        [InlineData("<@U9876Abbot> <@U9876Phil> is cool", "<@U9876Phil> is cool", "who", "is <@U9876Phil> cool", "")]
        [InlineData("<@U9876Abbot> <@U9876Phil> is not cool", "<@U9876Phil> is not cool", "who", "is not <@U9876Phil> cool", "")]
        [InlineData("<@U9876Abbot> <@U9876Phil> can edit foo", "<@U9876Phil> can edit foo", "can", "<@U9876Phil> edit foo", "")]
        [InlineData("<@U9876Abbot> <@U9876Phil> can not edit foo", "<@U9876Phil> can not edit foo", "can", "not <@U9876Phil> edit foo", "")]
        [InlineData("<@U9876Abbot> deepthought! add A deep thought", "deepthought! add A deep thought", "deepthought", "add A deep thought", "!")]
        public void ParsesPotentialSkillAndArguments(
            string message, string expectedTextAfterBotMention, string expectedSkill, string expectedArgs, string expectedSigil)
        {
            var result = ParsedMessage.Parse(message, "U9876Abbot", '.');

            Assert.Equal(result.OriginalMessage, message);
            Assert.Equal(expectedTextAfterBotMention, result.CommandText);
            Assert.True(result.IsBotCommand);
            Assert.Equal(expectedSkill, result.PotentialSkillName);
            Assert.Equal(expectedArgs, result.PotentialArguments);
            Assert.Equal(expectedSigil, result.Sigil);
        }

        [Theory]
        [InlineData(".joke", '.', "joke", "joke", "", "")]
        [InlineData("!joke", '!', "joke", "joke", "", "")]
        [InlineData("|joke", '|', "joke", "joke", "", "")]
        [InlineData("\\joke", '\\', "joke", "joke", "", "")]
        [InlineData(".tell me a joke", '.', "tell me a joke", "tell", "me a joke", "")]
        [InlineData(".deepthought add A deep thought", '.', "deepthought add A deep thought", "deepthought", "add A deep thought", "")]
        [InlineData(".rem paul’s address is UNDISCLOSED LOCATION", '.', "rem paul’s address is UNDISCLOSED LOCATION", "rem", "paul’s address is UNDISCLOSED LOCATION", "")]
        [InlineData(". <@U9876Phil> is cool", '.', "<@U9876Phil> is cool", "who", "is <@U9876Phil> cool", "")]
        [InlineData(". <@U9876Phil> is not cool", '.', "<@U9876Phil> is not cool", "who", "is not <@U9876Phil> cool", "")]
        [InlineData(". <@U9876Phil> can edit foo", '.', "<@U9876Phil> can edit foo", "can", "<@U9876Phil> edit foo", "")]
        [InlineData(". <@U9876Phil> can not edit foo", '.', "<@U9876Phil> can not edit foo", "can", "not <@U9876Phil> edit foo", "")]
        [InlineData(".deepthought! add A deep thought", '.', "deepthought! add A deep thought", "deepthought", "add A deep thought", "!")]
        public void WithShortcutCharacterParsesPotentialSkillAndArguments(
            string message,
            char shortcutCharacter,
            string expectedTextAfterBotMention,
            string expectedSkill,
            string expectedArgs,
            string expectedSigil)
        {
            var result = ParsedMessage.Parse(message, "U9876Abbot", shortcutCharacter);

            Assert.Equal(result.OriginalMessage, message);
            Assert.Equal(expectedTextAfterBotMention, result.CommandText);
            Assert.True(result.IsBotCommand);
            Assert.Equal(expectedSkill, result.PotentialSkillName);
            Assert.Equal(expectedArgs, result.PotentialArguments);
            Assert.Equal(expectedSigil, result.Sigil);
        }

        [Fact]
        public void WhenShortcutCharacterIsSpaceDisableShortcut()
        {
            var result = ParsedMessage.Parse(" joke", "U9876Abbot", ' ');

            Assert.Equal(" joke", result.OriginalMessage);
            Assert.Equal(string.Empty, result.CommandText);
            Assert.False(result.IsBotCommand);
            Assert.Equal(string.Empty, result.PotentialSkillName);
            Assert.Equal(string.Empty, result.PotentialArguments);
        }

        [Theory]
        [InlineData("joke")]
        [InlineData("tell me a joke")]
        [InlineData("deepthought add A deep thought")]
        public void ParsesNonCommandsToBotCorrectly(string message)
        {
            var result = ParsedMessage.Parse(message, "U9876Abbot", '.');

            Assert.False(result.IsBotCommand);
            Assert.Equal(result.OriginalMessage, message);
            Assert.Empty(result.CommandText);
            Assert.Empty(result.PotentialSkillName);
            Assert.Empty(result.PotentialArguments);
        }
    }

    public class TheCreateMethod
    {
        [Fact]
        public void CreatesParsedMessageWithPatterns()
        {
            var patterns = new List<SkillPattern>
            {
                new() {Skill = new Skill {Name = "pug" }},
                new() {Skill = new Skill {Name = "yell" }}
            };

            var result = ParsedMessage.Create(patterns, "The original message");

            Assert.Equal(RemoteSkillCallSkill.SkillName, result.PotentialSkillName);
            Assert.Equal(2, result.Patterns.Count);
            Assert.Equal("pug", result.Patterns[0].Skill.Name);
            Assert.Equal("yell", result.Patterns[1].Skill.Name);
        }
    }
}
