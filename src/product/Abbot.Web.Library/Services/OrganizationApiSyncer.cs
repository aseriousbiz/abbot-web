using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Services;

/// <summary>
/// Class used to keep an organization updated from the Slack API.
/// </summary>
public interface IOrganizationApiSyncer
{
    /// <summary>
    /// Updates all the organization's rooms (such as room membership status, etc) based on calling the Slack API.
    /// </summary>
    /// <returns></returns>
    Task UpdateRoomsFromApiAsync(Organization organization);
}

public class OrganizationApiSyncer : IOrganizationApiSyncer
{
    static readonly ILogger<OrganizationApiSyncer> Log = ApplicationLoggerFactory.CreateLogger<OrganizationApiSyncer>();

    readonly ISlackApiClient _slackApiClient;
    readonly ISlackResolver _slackResolver;
    readonly IOrganizationRepository _organizationRepository;

    public OrganizationApiSyncer(
        ISlackApiClient slackApiClient,
        ISlackResolver slackResolver,
        IOrganizationRepository organizationRepository)
    {
        _slackApiClient = slackApiClient;
        _slackResolver = slackResolver;
        _organizationRepository = organizationRepository;
    }

    // This method is not on the interface. It's here so we can easily enqueue updates in the background.
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task UpdateRoomsFromApiAsync(int organizationId)
    {
        var organization = await _organizationRepository.GetAsync(organizationId).Require();
        using var orgScope = Log.BeginOrganizationScope(organization);
        await UpdateRoomsFromApiAsync(organization);
    }

    public async Task UpdateRoomsFromApiAsync(Organization organization)
    {
        var apiToken = organization.RequireAndRevealApiToken();

        // Get the bot's membership status.
        var response = await _slackApiClient.GetAllUsersConversationsAsync(
            apiToken,
            user: organization.PlatformBotUserId,
            types: "public_channel,private_channel");

        if (!response.Ok)
        {
            Log.ErrorCallingSlackApi(response.ToString());
            return;
        }

        var channels = response.Body;
        var channelsBotIsInLookup = channels.Select(c => c.Id).ToHashSet();

        var rooms = await _organizationRepository.EnsureRoomsLoadedAsync(organization);
        var deletedRooms = rooms.Where(r => !channelsBotIsInLookup.Contains(r.PlatformRoomId));

        // Remove bot from deleted rooms.
        foreach (var roomBotIsNoLongerIn in deletedRooms)
        {
            roomBotIsNoLongerIn.BotIsMember = false;
        }

        await _organizationRepository.SaveChangesAsync();

        // Make sure bot is in all the rooms it should be and update room info.
        foreach (var channel in channels)
        {
            ConversationInfo conversation = new ConversationInfo(channel) { IsMember = true };
            await _slackResolver.ResolveAndUpdateRoomAsync(conversation, organization);
        }

        // Get public channels
        response = await _slackApiClient.Conversations.GetAllConversationsAsync(
            apiToken,
            types: "public_channel",
            excludeArchived: true);

        if (!response.Ok)
        {
            Log.ErrorCallingSlackApi(response.ToString());
            return;
        }

        var channelsBotIsNotIn = response.Body.Where(c => !channelsBotIsInLookup.Contains(c.Id));

        // Add public rooms bot is not in.
        foreach (var channel in channelsBotIsNotIn)
        {
            ConversationInfo conversation = new ConversationInfo(channel) { IsMember = false };
            await _slackResolver.ResolveAndUpdateRoomAsync(conversation, organization);
        }
    }
}
