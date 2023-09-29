using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;

public class ZendeskLinkTests
{
    [Fact]
    public void SerializesTicketLink()
    {
        var integrationId = (Id<Integration>)12;
        var link = new ZendeskTicketLink("sub", 123);

        var json = link.ToJson();

        Assert.Equal(
            $$"""
            {"TicketId":123,"ApiUrl":"https://sub.zendesk.com/api/v2/tickets/123.json","WebUrl":"https://sub.zendesk.com/agent/tickets/123","Subdomain":"sub","IntegrationType":"Zendesk"}
            """,
            json);
    }

    [Fact]
    public void SerializesTicketLinkWithStatus()
    {
        var integrationId = (Id<Integration>)12;
        var link = new ZendeskTicketLink("sub", 123) { Status = "Closed" };

        var json = link.ToJson();

        Assert.Equal(
            $$"""
            {"TicketId":123,"ApiUrl":"https://sub.zendesk.com/api/v2/tickets/123.json","WebUrl":"https://sub.zendesk.com/agent/tickets/123","Status":"Closed","Subdomain":"sub","IntegrationType":"Zendesk"}
            """,
            json);
    }
}
