using System.Threading.Tasks;
using Newtonsoft.Json;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.Events;
using Serious.Slack.Tests;
using Xunit;

public class ElementDeserializationTests
{
    [Fact]
    public async Task UsesCustomConverterWhenDeserializedAsEventInterface()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_home_opened.json");

        var result = JsonConvert.DeserializeObject<IEventEnvelope<EventBody>>(json);

        var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("home", envelope.Event.Tab);
        Assert.Empty(((IPropertyBag)envelope).AdditionalProperties);
    }

    [Fact]
    public async Task UsesCustomConverterWhenDeserializedAsEventConcreteType()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_home_opened.json");

        var result = JsonConvert.DeserializeObject<EventEnvelope<AppHomeOpenedEvent>>(json);

        var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("home", envelope.Event.Tab);
    }

    [Fact]
    public async Task UsesCustomConverterWhenDeserializedAsElement()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_home_opened.json");

        var result = JsonConvert.DeserializeObject<Element>(json);

        var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("home", envelope.Event.Tab);
    }

    [Fact]
    public async Task UsesCustomConverterWhenDeserializingMessageAsEventEnvelope()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_mention.json");

        var result = JsonConvert.DeserializeObject<IEventEnvelope<EventBody>>(json);

        var envelope = Assert.IsType<EventEnvelope<AppMentionEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("app_mention", envelope.Event.Type);
    }

    [Fact]
    public async Task UsesCustomConverterWhenDeserializingMessageAsElement()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_mention.json");

        var result = JsonConvert.DeserializeObject<IElement>(json);

        var envelope = Assert.IsType<EventEnvelope<AppMentionEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("app_mention", envelope.Event.Type);
    }

    [Fact]
    public async Task UsesCustomConverterWhenDeserializedAsIElement()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource("app_home_opened.json");

        var result = JsonConvert.DeserializeObject<IElement>(json);

        var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("home", envelope.Event.Tab);
    }

    [Fact]
    public void CanBeSerialized()
    {
        var instance = new EventEnvelope<AppHomeOpenedEvent>
        {
            Token = "Token",
            Event = new AppHomeOpenedEvent
            {
                Tab = "home"
            }
        };
        var json = JsonConvert.SerializeObject(instance);

        var result = JsonConvert.DeserializeObject<IElement>(json);

        Assert.NotNull(result);
        var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
        Assert.Equal("event_callback", envelope.Type);
        Assert.Equal("app_home_opened", envelope.Event.Type);
        Assert.Equal("home", envelope.Event.Tab);
    }

    [Fact]
    public async Task DeserializesTeamInfoResponse()
    {
        var json = await EmbeddedResourceHelper.ReadSerializationResource($"team.info.json");

        var result = JsonConvert.DeserializeObject<TeamInfoResponse>(json);

        var response = Assert.IsType<TeamInfoResponse>(result);
        Assert.True(response.Ok);
        Assert.Equal("Serious Sandbox 2", response.Body.Name);
        Assert.Equal("E02V6NXUF2Q", response.Body.EnterpriseId);
        Assert.Equal("A Serious Grid", response.Body.EnterpriseName);
        Assert.Equal("els1411b", response.Body.EnterpriseDomain);
    }
}
