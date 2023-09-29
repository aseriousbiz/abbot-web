
using System;
using System.Threading.Tasks;
using Serious.Abbot.Exceptions;
using Serious.Slack.Abstractions;
using Serious.Slack.Tests;
using Xunit;

public class SlackEventDeserializationExceptionTests
{
    public class TheCreateMethod
    {
        [Fact]
        public async Task AttemptsToDeserializePayloadToSimplerType()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.invalid-json.json");

            var exception = SlackEventDeserializationException.Create(json, typeof(IElement), "Protected", new Exception());

            Assert.NotNull(exception.EventEnvelope);
            Assert.Equal("event_callback", exception.EventEnvelope.Type);
            Assert.Equal("Ev00000001", exception.EventEnvelope.EventId);
            Assert.Equal("T013108BYLS", exception.EventEnvelope.TeamId);
            Assert.Equal("C01A3DGTSP9", exception.EventEnvelope?.Event?.Channel);
            Assert.Equal("U012LKJFG0P", exception.EventEnvelope?.Event?.User);
        }

        [Fact]
        public void HandlesCompletelyBadJson()
        {
            var json = "{this is garbage";

            var exception = SlackEventDeserializationException.Create(json, typeof(IElement), "Protected", new Exception());

            Assert.Null(exception.EventEnvelope);
            Assert.Equal("Could not deserialize JSON to IElement.\n\nProtected", exception.Message);
        }
    }
}
