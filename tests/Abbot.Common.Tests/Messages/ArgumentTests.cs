using Serious.Abbot.Messages;
using Xunit;

public class ArgumentTests
{
    public class TheTryParseMentionMethod
    {
        [Theory]
        [InlineData("<@U013WCHH9NU>", "U013WCHH9NU", true)]
        [InlineData("<U013WCHH9NU>", null, false)]
        [InlineData("@haacked", null, false)]
        [InlineData("<at>haacked</at>", null, false)]
        public void ParsesMentionArgument(
            string value,
            string? expectedPlatformUserId,
            bool expectedResult)
        {
            var result = Argument.TryParseMention(value, out var platformUserId);

            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedPlatformUserId, platformUserId);
        }
    }
}
