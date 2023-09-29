using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.Events;

namespace Serious.TestHelpers
{
    public static class FakeTurnContextFactory
    {
        public static ITurnContext<IMessageActivity> CreateTurnContext(
            string type = "message",
            string? channelId = null,
            ChannelAccount? sender = null,
            ChannelAccount? recipient = null,
            string text = "",
            DateTimeOffset? timestamp = null,
            IEnumerable<ChannelAccount>? mentions = null,
            string? textFormat = null,
            object? channelData = null,
            string? activityName = null)
        {
            mentions ??= new List<ChannelAccount>();

            var activity = new Activity
            {
                Type = type,
                From = sender ?? new ChannelAccount("U123:T013108BYLS", "Somebody You Used To Know"),
                ChannelId = channelId ?? "slack",
                Timestamp = timestamp,
                Recipient = recipient ?? new ChannelAccount(
                    "B0136HA6VJ6",
                    "abbot"),
                Entities = mentions.Select(a => new Entity
                {
                    Type = "mention",
                    Properties = JObject.Parse($@"{{""mentioned"": {{""id"": ""{a.Id}"", ""name"": ""{a.Name}"" }} }}")
                }).ToList(),
                Text = text,
                TextFormat = textFormat,
                ChannelData = channelData is null
                    ? CreateChannelData()
                    : JObject.FromObject(channelData),
                Name = activityName
            };
            return new FakeTurnContext(activity);
        }

        static object? CreateChannelData()
        {
            return new SlackChannelData
            {
                SlackMessage = new EventEnvelope<EventBody>
                {
                    Authorizations = new[]
                    {
                        new Authorization
                        {
                            TeamId = "T001",
                            UserId = "U001",
                            IsBot = true
                        }
                    }
                }
            };
        }
    }
}
