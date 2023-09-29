using Serious.Abbot.Integrations.HubSpot;

public class HubSpotLinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(90)]
    public void SerializesTicketLink(int? threadId)
    {
        var link = new HubSpotTicketLink(1357, "2468") { ThreadId = threadId };

        var json = link.ToJson();

        var expectedThreadIdJson = (threadId is null ? "" : $""","ThreadId":{threadId}""");
        Assert.Equal(
            $$"""
            {"TicketId":"2468","ApiUrl":"https://api.hubapi.com/crm/v3/objects/tickets/2468","WebUrl":"https://app.hubspot.com/contacts/1357/ticket/2468"{{expectedThreadIdJson}},"HubId":1357,"IntegrationType":"HubSpot"}
            """,
            json);
    }
}
