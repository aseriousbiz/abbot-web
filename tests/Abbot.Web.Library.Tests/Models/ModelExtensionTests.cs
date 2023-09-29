using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Xunit;

public class ModelExtensionTests
{
    public class TheFormatMentionMethod
    {
        [Fact]
        public void FormatsMentionCorrectly()
        {
            var user = new User
            {
                PlatformUserId = "U1234",
                DisplayName = "haacked"
            };

            var result = user.FormatMention();

            Assert.Equal("<@U1234>", result);
        }
    }
}
