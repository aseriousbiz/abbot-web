using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Execution;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class BotUtilitiesTests
{
    public class TheGetGeocodeAsyncMethod
    {
        [Fact]
        public async Task CallsGeocodeApiCorrectly()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/geo?address=The+Moon&includeTimezone=False");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new Location
            {
                Coordinate = new Coordinate(42.0, 46.1)
            });
            var utilities = new BotUtilities(apiClient, new FakeBrainSerializer());

            var result = await utilities.GetGeocodeAsync("The Moon");

            Assert.NotNull(result?.Coordinate);
            Assert.Equal(42.0, result.Coordinate.Latitude);
            Assert.Equal(46.1, result.Coordinate.Longitude);
        }
    }

    public class TheCreateRandomMethod
    {
        [Fact]
        public void DoesNotCreateSameValueTwice()
        {
            var apiClient = new FakeSkillApiClient(42);
            var utilities = new BotUtilities(apiClient, new FakeBrainSerializer());

            var groupedValues = Enumerable
                .Range(0, 10)
                .Select(_ => utilities.CreateRandom())
                .GroupBy(r => r);

            Assert.True(groupedValues.All(g => g.Count() == 1));
        }
    }

    public class TheTryParseShareUrlMethod
    {
        [Theory]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF/p1639006342178800?thread_ts=1639006311.178500&cid=C012ZJGPYTF", ChatAddressType.Room, "C012ZJGPYTF", "1639006311.178500")]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF/p1639006311178500", ChatAddressType.Room, "C012ZJGPYTF", "1639006311.178500")]
        [InlineData("https://aseriousbiz.slack.com/archives/C012ZJGPYTF", ChatAddressType.Room, "C012ZJGPYTF", null)]
        [InlineData("https://aseriousbiz.slack.com/archives/G0987654321", ChatAddressType.Room, "G0987654321", null)]
        [InlineData("https://aseriousbiz.slack.com/archives/D02LV16PBE3", ChatAddressType.User, "D02LV16PBE3", null)]
        [InlineData("https://aseriousbiz.slack.com/team/U02EMN2AYGH", ChatAddressType.User, "U02EMN2AYGH", null)]
        public void ParsesShareUrls(string url, ChatAddressType type, string id, string threadId)
        {
            var apiClient = new FakeSkillApiClient(42);
            var utilities = new BotUtilities(apiClient, new FakeBrainSerializer());

            Assert.True(utilities.TryParseSlackUrl(url, out var conversation));
            Assert.NotNull(conversation);
            Assert.Equal(type, conversation.Address.Type);
            Assert.Equal(id, conversation.Address.Id);
            Assert.Equal(threadId, conversation.Address.ThreadId);
        }
    }
}
