using System.Diagnostics;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.Zendesk;

namespace Serious.Abbot.Integrations;

public static class CreateTicketFormModal
{
    public static InteractionCallbackInfo For(Integration integration)
    {
        return integration.Type switch
        {
            IntegrationType.Zendesk =>
                InteractionCallbackInfo.For<CreateZendeskTicketFormModal>($"{integration.Id}"),
            IntegrationType.HubSpot =>
                InteractionCallbackInfo.For<CreateHubSpotTicketFormModal>($"{integration.Id}"),
            IntegrationType.GitHub =>
                InteractionCallbackInfo.For<CreateGitHubIssueFormModal>($"{integration.Id}"),
            IntegrationType.Ticketing =>
                InteractionCallbackInfo.For<CreateMergeDevTicketFormModal>($"{integration.Id}"),
            _ => throw new UnreachableException()
        };
    }
}
