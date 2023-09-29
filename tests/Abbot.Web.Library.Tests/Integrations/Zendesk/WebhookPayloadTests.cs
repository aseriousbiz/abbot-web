using Newtonsoft.Json;
using Serious.Abbot.Integrations.Zendesk;
using Xunit;

namespace Abbot.Web.Library.Tests.Integrations.Zendesk;

public class WebhookPayloadTests
{
    const string ExamplePayload = @"{
    ""TicketUrl"": ""d3v-aseriousbusiness.zendesk.com/agent/tickets/33"",
    ""TicketId"": 33
}";

    [Fact]
    public void CanDeserializeExamplePayload()
    {
        var payload = JsonConvert.DeserializeObject<WebhookPayload>(ExamplePayload);
        Assert.NotNull(payload);
        Assert.Equal("d3v-aseriousbusiness.zendesk.com/agent/tickets/33", payload.TicketUrl);
        Assert.Equal(33, payload.TicketId);
    }
}
