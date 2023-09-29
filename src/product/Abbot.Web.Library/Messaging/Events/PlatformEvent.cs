using System.Security.Claims;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Cryptography;

namespace Serious.Abbot.Events;

/// <summary>
/// The payload for the event when the bot is added to Slack.
/// </summary>
/// <remarks>
/// The event is handled in <see cref="MetaBot" />. The actual install code is in
/// <see cref="OrganizationRepository"/>.
/// </remarks>
/// <param name="PlatformId">The team or org id on the chat platform. For Slack this typically starts with "T" such as "T0123456789".</param>
/// <param name="PlatformType">The platform type of the skill such as Slack.</param>
/// <param name="AppId">The App that was installed. This is usually Abbot, but could be a while-labeled version of Abbot in the future.</param>
/// <param name="BotId">The Id of the bot. For Slack this is something like "B01234" and not the Bot user id.</param>
/// <param name="BotUserId">The User Id of the bot. For most platforms, this is null. For Slack, this is an Id for the bot user in the form "U#####"</param>
/// <param name="BotName">The name of the bot.</param>
/// <param name="BotAppName">For Slack, this is Abbot's bot App name.</param>
/// <param name="BotAvatar">The URL for the bot's avatar.</param>
/// <param name="Avatar">The URL for the organization.</param>
/// <param name="Name">The name of the organization.</param>
/// <param name="Slug">A unique slug to use for the organization. For Slack this is the sub-domain. For other platforms, it's the PlatformId.</param>
/// <param name="Domain">The Slack hostname for the organization.</param>
/// <param name="OAuthScopes">The set of OAuth scopes granted to Abbot.</param>
/// <param name="ApiToken">The ApiToken in the channel data that Bot Service gives us.</param>
/// <param name="EnterpriseId">The Enterprise Grid Id if the organization is part of an Enterprise Grid.</param>
public record InstallEvent(
    string PlatformId,
    PlatformType PlatformType,
    string BotId,
    string BotName,
    string Name,
    string Slug,
    SecretString? ApiToken,
    string? EnterpriseId,
    string? Domain = null,
    string? OAuthScopes = null,
    string? BotAppName = null,
    string? BotAvatar = null,
    string? Avatar = null,
    string? BotUserId = null,
    string? AppId = null,
    ClaimsPrincipal? Installer = null
    ) : IOrganizationIdentifier;

/// <summary>
/// Represents a strongly typed normalized event that occurred on a chat platform.
/// </summary>
/// <param name="Payload">The event payload.</param>
/// <param name="TriggerId">A short-lived ID that can be used to open Modals in Slack. Not all events provide this.</param>
/// <param name="Bot">The bot user.</param>
/// <param name="Timestamp">The date and time when this message was received.</param>
/// <param name="Responder">Used to respond to the event in chat.</param>
/// <param name="From">The <see cref="Member"/> that initiated the event or message.</param>
/// <param name="Room">The <see cref="Room"/> the event occurred in, if applicable.</param>
/// <param name="Organization">The organization where this event originated.</param>
public record PlatformEvent<TPayload>(
    TPayload Payload,
    string? TriggerId,
    BotChannelUser Bot,
    DateTimeOffset Timestamp,
    IResponder Responder,
    Member From,
    Room? Room,
    Organization Organization) : IPlatformEvent<TPayload>
{
    object? IPlatformEvent.Payload => Payload;

    /// <summary>
    /// Gets the <see cref="TargetingContext"/> that should be used to evaluate this actor against the feature filters
    /// </summary>
    public TargetingContext GetTargetingContext() =>
        FeatureHelper.CreateTargetingContext(Organization, From.User.PlatformUserId);
}
