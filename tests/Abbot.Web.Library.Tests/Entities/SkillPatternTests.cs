using Serious.Abbot.Entities;
using Serious.TestHelpers;

public class SkillPatternTests
{
    public class TheMatchMethod
    {
        [Theory]
        [InlineData("This MESSAGE will self destruct", "message", PatternType.Contains)]
        [InlineData("MESSAGE", "message", PatternType.ExactMatch)]
        [InlineData("MEssAGE", "eSs", PatternType.Contains)]
        [InlineData("mesSage to my friends", "MeSs", PatternType.StartsWith)]
        [InlineData("the messaGe", "age", PatternType.EndsWith)]
        [InlineData("this is the 42nd message", @"\d{2}ND", PatternType.RegularExpression)]
        public void ReturnsTrueWhenPatternMatchesCaseInsensitively(string message, string pattern, PatternType patternType)
        {
            var skillPattern = new SkillPattern
            {
                Pattern = pattern,
                PatternType = patternType
            };
            var chatPlatformMessage = new FakePatternMatchableMessage(message);

            var result = skillPattern.Match(chatPlatformMessage);

            Assert.True(result);
        }

        [Theory]
        [InlineData("Message for you", "message", PatternType.ExactMatch)]
        [InlineData("Message", "message for you", PatternType.ExactMatch)]
        [InlineData("message", "message", PatternType.None)]
        [InlineData("message", "less", PatternType.StartsWith)]
        [InlineData("message", "tage", PatternType.EndsWith)]
        [InlineData("this is the 42nd message", @"^\d{2}nd$", PatternType.RegularExpression)]
        public void ReturnsFalseWhenPatternDoesNotMatch(string message, string pattern, PatternType patternType)
        {
            var skillPattern = new SkillPattern
            {
                Pattern = pattern,
                PatternType = patternType
            };
            var chatPlatformMessage = new FakePatternMatchableMessage(message);

            var result = skillPattern.Match(chatPlatformMessage);

            Assert.False(result);
        }

        [Theory]
        [InlineData("MESSAGE", "message", PatternType.ExactMatch)]
        [InlineData("MESSAGE", "message", PatternType.Contains)]
        [InlineData("MEssAGE", "eSs", PatternType.Contains)]
        [InlineData("mesSage to my friends", "MeSs", PatternType.StartsWith)]
        [InlineData("the messaGe", "age", PatternType.EndsWith)]
        [InlineData("this is the 42nd message", @"\d{2}ND", PatternType.RegularExpression)]
        public void ReturnsFalseWhenPatternMatchesButCaseSensitivityDoesNot(string message, string pattern, PatternType patternType)
        {
            var skillPattern = new SkillPattern
            {
                Pattern = pattern,
                PatternType = patternType,
                CaseSensitive = true
            };
            var chatPlatformMessage = new FakePatternMatchableMessage(message);

            var result = skillPattern.Match(chatPlatformMessage);

            Assert.False(result);
        }
    }
}
