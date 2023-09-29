using System;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using NSubstitute;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class ActivityExtensionsTests
{
    public class TheIsMessageMethod
    {
        [Theory]
        [InlineData("message", true)]
        [InlineData("app_mention", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("rando", false)]
        public void ReturnsTrueForMessagesAndMentions(string activityType, bool isMessage)
        {
            var turnContext = Substitute.For<ITurnContext>();
            turnContext.Activity.Returns(new Activity { Type = activityType });

            var result = turnContext.IsMessage();

            Assert.Equal(isMessage, result);
        }

        [Theory]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("rando", true)]
        public void ReturnsTrueITurnContextOfMessageActivity(string activityType, bool isMessage)
        {
            var turnContext = new FakeTurnContext(new Activity { Type = activityType });

            var result = turnContext.IsMessage();

            Assert.Equal(isMessage, result);
        }
    }
}
