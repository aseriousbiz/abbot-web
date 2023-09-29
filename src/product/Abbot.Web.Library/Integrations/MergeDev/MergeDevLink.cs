using Serious.Abbot.Entities;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Integrations.MergeDev;

public abstract record MergeDevLink(
    [property:Newtonsoft.Json.JsonIgnore]
    [property:System.Text.Json.Serialization.JsonIgnore]
    Id<Integration> IntegrationId,
    string IntegrationSlug,
    string IntegrationName,
    string Id)
    : IntegrationLink
{
    public override IntegrationType IntegrationType => IntegrationType.Ticketing;
}

public record MergeDevTicketLink(
    Id<Integration> IntegrationId,
    string IntegrationSlug,
    string IntegrationName,
    string Id,
    [property:Newtonsoft.Json.JsonIgnore]
    [property:System.Text.Json.Serialization.JsonIgnore]
    string? ExternalUrl
    ) : MergeDevLink(IntegrationId, IntegrationSlug, IntegrationName, Id)
{
    public override Uri ApiUrl => new($"https://api.merge.dev/api/ticketing/v1/tickets/{Id}");
    public override Uri? WebUrl => ExternalUrl is null ? null : new(ExternalUrl);

    public static MergeDevTicketLink? From(ConversationLink? conversationLink) =>
        conversationLink is { ExternalId: not null }
        && FromJson<Settings>(conversationLink.Settings) is { } linkSettings
        ? new MergeDevTicketLink(
            linkSettings.IntegrationId,
            linkSettings.IntegrationSlug,
            linkSettings.IntegrationName,
            conversationLink.ExternalId,
            linkSettings.ExternalWebUrl)
        : null;

    public record Settings(
        Id<Integration> IntegrationId,
        string IntegrationSlug,
        string IntegrationName,
        string? ExternalWebUrl
        ) : JsonSettings;
}
