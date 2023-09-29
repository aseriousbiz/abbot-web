using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hangfire;
using Newtonsoft.Json;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot.Models;

namespace Serious.Abbot.Integrations.HubSpot;

public partial class HubSpotLinker
{
    /// <summary>
    /// When posting a property that accepts multiple values, each value is separated by a ;.
    /// </summary>
    public const char HubSpotValueDelimiter = ';';

    public async Task<HubSpotTicket?> CreateTicketAsync(
        Integration integration,
        HubSpotSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor)
    {
        if (settings is not { TicketPipelineId: { } pipelineId, NewTicketPipelineStageId: { } stageId })
        {
            throw new TicketConfigurationException(
                "Your organization does not have a Ticket Pipeline and Pipeline Stage configured.");
        }

        var companyLink = HubSpotCompanyLink.Parse(
            (string?)properties.GetValueOrDefault("roomIntegrationLink"));

        using var __ = Log.BeginHubSpotCompanyScope(companyLink);

        var client = await _hubSpotClientFactory.CreateClientAsync(integration, settings);

        var contactLink = await _hubSpotResolver.ResolveHubSpotContactAsync(
            client,
            integration,
            conversation.StartedBy);

        static string? ValueToString(object? value) => value switch
        {
            null => null,
            IEnumerable<string> strings => string.Join(HubSpotValueDelimiter, strings),
            _ => value.ToString()
        };

        // Transform properties to HubSpot format
        var hubspotProperties = properties
            .Where(p => p.Key is not "roomIntegrationLink")
            .ToDictionary(p => p.Key, p => ValueToString(p.Value));

        var ticket = new CreateOrUpdateTicket
        {
            Associations = BuildAssociations(companyLink, contactLink),
            Properties = new Dictionary<string, string?>(hubspotProperties)
            {
                ["hs_pipeline"] = pipelineId.ToStringInvariant(),
                ["hs_pipeline_stage"] = stageId.ToStringInvariant(),
            }
        };

        var org = conversation.Organization;
        var portalId = long.Parse(integration.ExternalId.Require(), CultureInfo.InvariantCulture);
        var formGuid = (await _settingsManager.GetHubSpotFormSettingsAsync(org))?.HubSpotFormGuid;

        return formGuid is null or ""
            ? await client.CreateTicketAsync(ticket)
            : await SubmitFormAsync(hubspotProperties, settings, portalId, formGuid, conversation, integration, org, actor);
    }

    static IList<CreateHubSpotAssociation>? BuildAssociations(HubSpotCompanyLink? companyLink, HubSpotContactLink? contactLink)
    {
        List<CreateHubSpotAssociation>? associations = null;
        if (companyLink is not null)
        {
#pragma warning disable CA1508
            associations ??= new();
#pragma warning restore CA1508
            associations.Add(new(
                new() { Id = companyLink.CompanyId },
                new CreateHubSpotAssociationType[]
                {
                    new(HubSpotAssociationTypeId.TicketToCompany),
                }));
        }
        if (contactLink is not null)
        {
            associations ??= new();
            associations.Add(new(
                new() { Id = contactLink.ContactId },
                new CreateHubSpotAssociationType[]
                {
                    new(HubSpotAssociationTypeId.TicketToContact),
                }));
        }
        return associations;
    }

    public string? GetPendingTicketUrl(Conversation conversation, Member actor) =>
        _urlGenerator.PendingTicketPage(conversation, IntegrationType.HubSpot, actor).ToString();

    public string? GetUserInfo(ApiException apiException)
    {
        // Let's try to extract something useful to report to the user from the ApiException.Conte.
        if (apiException.Content is not { Length: > 0 } content || content[0] is not '{')
        {
            return apiException.Content;
        }

        var errorDetails = JsonConvert.DeserializeObject<HubspotApiError>(content);
        const string messagePrefix = "Property values were not valid: ";
        return errorDetails switch
        {
            { Category: "VALIDATION_ERROR" } when errorDetails.Message.StartsWith(messagePrefix, StringComparison.Ordinal)
                => errorDetails.Message[messagePrefix.Length..],
            { Errors.Count: > 0 } => string.Join('\n', errorDetails.Errors.Select(e => e.Message)),
            _ => apiException.Content
        };
    }

    async Task<HubSpotTicket?> SubmitFormAsync(
        IReadOnlyDictionary<string, string?> properties,
        HubSpotSettings settings,
        long portalId,
        string formGuid,
        Conversation conversation,
        Integration integration,
        Organization organization,
        Member actor)
    {
        var tokenFieldName = (await _settingsManager.GetHubSpotFormSettingsAsync(organization))?.TokenFormField;

        var searchToken = CreateSearchToken(conversation);

        string AdjustValue(string key, string? value)
        {
            if (key == tokenFieldName)
            {
                // We want to embed a secret token we can easily round trip.
                return $"{value}\n\n{searchToken}";
            }

            return value ?? string.Empty;
        }

        var fields = properties.Select(
            kvp => new HubSpotField(kvp.Key, AdjustValue(kvp.Key, kvp.Value)));
        var request = new HubSpotFormSubmissionRequest(fields.ToList());
        var hubSpotFormsClient = await _hubSpotClientFactory.CreateFormsClientAsync(integration, settings);
        await hubSpotFormsClient.SubmitAsync(portalId, formGuid, request);

        // Schedule a job to try and link the ticket that should get created as a result of this form submission
        // to the conversation.
        _backgroundJobClient.Schedule<HubSpotLinker>(
            linker => linker.LinkPendingConversationTicketAsync(
                conversation,
                actor,
                actor.Organization,
                0),
            TimeSpan.FromSeconds(2));
        return null;
    }
}
