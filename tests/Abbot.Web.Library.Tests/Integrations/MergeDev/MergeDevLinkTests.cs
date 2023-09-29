using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.MergeDev;

public class MergeDevLinkTests
{
    [Fact]
    public void SerializesTicketLink()
    {
        var integrationId = (Id<Integration>)12;
        var link = new MergeDevTicketLink(integrationId, "slug", "name", "guid", "https://example.com/123");

        var json = link.ToJson();

        Assert.Equal(
            $$"""
            {"ApiUrl":"https://api.merge.dev/api/ticketing/v1/tickets/guid","WebUrl":"https://example.com/123","IntegrationSlug":"slug","IntegrationName":"name","Id":"guid","IntegrationType":"Ticketing"}
            """,
            json);
    }
}
