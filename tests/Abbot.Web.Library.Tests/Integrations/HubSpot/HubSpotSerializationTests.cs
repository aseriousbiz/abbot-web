using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serious;
using Serious.Abbot.Integrations.HubSpot;
using Xunit;

public class HubSpotSerializationTests
{
    [Fact]
    public async Task DeserializesNewMessagePayload()
    {
        var payloadJson = (await GetType().ReadAssemblyResourceAsync("Serialization.HubSpot.Webhook.NewMessage.Payload.json")).Trim();

        var payloads = JsonConvert.DeserializeObject<IReadOnlyList<HubSpotWebhookPayload>>(payloadJson);

        Assert.NotNull(payloads);
        var payload = Assert.Single(payloads);
        Assert.Equal(1376226851, payload.EventId);
        Assert.Equal(1870311, payload.SubscriptionId);
        Assert.Equal(22761544, payload.PortalId);
        Assert.Equal(1098266, payload.AppId);
        Assert.Equal(1670373142223, payload.OccurredAt);
        Assert.Equal("conversation.newMessage", payload.SubscriptionType);
        Assert.Equal(1, payload.AttemptNumber);
        Assert.Equal(3624100517, payload.ObjectId);
        Assert.Equal("5bc511c6161c41ba811829eb460a83b8", payload.MessageId);
        Assert.Equal("MESSAGE", payload.MessageType);
        Assert.Equal("NEW_MESSAGE", payload.ChangeFlag);
    }

    [Fact]
    public async Task CanDeserializeMessage()
    {
        var messageJson = (await GetType().ReadAssemblyResourceAsync("Serialization.HubSpot.Message.json")).Trim();

        var message = JsonConvert.DeserializeObject<HubSpotMessage>(messageJson);

        Assert.NotNull(message);
        Assert.Equal("HUBSPOT", message.Client.ClientType);
        Assert.Equal("Ok, let's see if the webhook subscription works.", message.Text);
        Assert.Equal("OUTGOING", message.Direction);
        var sender = Assert.Single(message.Senders);
        Assert.Equal("A-46685293", sender.ActorId);
        Assert.Equal("Phil Haack", sender.Name);
        Assert.Equal("FROM", sender.SenderField);
        Assert.Equal("HS_EMAIL_ADDRESS", sender.DeliveryIdentifier.Type);
        Assert.Equal("support@22761544.hs-inbox.com", sender.DeliveryIdentifier.Value);
        var recipient = Assert.Single(message.Recipients);
        Assert.Equal("E-haacked@example.com", recipient.ActorId);
        Assert.Equal("TO", recipient.RecipientField);
        var recipientDeliveryIdentifier = Assert.Single(recipient.DeliveryIdentifiers);
        Assert.Equal("HS_EMAIL_ADDRESS", recipientDeliveryIdentifier.Type);
        Assert.Equal("haacked@example.com", recipientDeliveryIdentifier.Value);
    }
}
