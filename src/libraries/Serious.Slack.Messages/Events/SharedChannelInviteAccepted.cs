using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// A shared channel invite was accepted
/// </summary>
[Element("shared_channel_invite_accepted")]
public sealed record SharedChannelInviteAccepted() : SharedChannelInviteEvent("shared_channel_invite_accepted")
{
    /// <summary>
    /// Whether or not approval is required for this shared channel.
    /// </summary>
    [JsonProperty("approval_required")]
    [JsonPropertyName("approval_required")]
    public bool ApprovalRequired { get; init; }

    /// <summary>
    /// The user that accepted the invitation.
    /// </summary>
    [JsonProperty("accepting_user")]
    [JsonPropertyName("accepting_user")]
    public UserInfo? AcceptingUser { get; init; }
}

/// <summary>
/// A shared channel invite was accepted
/// </summary>
[Element("shared_channel_invite_approved")]
public sealed record SharedChannelInviteApproved() : SharedChannelInviteEvent("shared_channel_invite_approved")
{
    /// <summary>
    /// The Id of the team that approved this invitation.
    /// </summary>
    [JsonProperty("approving_team_id")]
    [JsonPropertyName("approving_team_id")]
    public required string ApprovingTeamId { get; init; }

    /// <summary>
    /// The user that accepted the invitation, if Slack knows anything about them.
    /// </summary>
    [JsonProperty("approving_user")]
    [JsonPropertyName("approving_user")]
    public UserInfo? ApprovingUser { get; init; }
}

/// <summary>
/// A shared channel invite was declined
/// </summary>
[Element("shared_channel_invite_declined")]
public sealed record SharedChannelInviteDeclined() : SharedChannelInviteEvent("shared_channel_invite_declined")
{
    /// <summary>
    /// The Id of the team that declined this invitation.
    /// </summary>
    [JsonProperty("declining_team_id")]
    [JsonPropertyName("declining_team_id")]
    public required string DecliningTeamId { get; init; }

    /// <summary>
    /// The user that declined the invitation.
    /// </summary>
    [JsonProperty("declining_user")]
    [JsonPropertyName("declining_user")]
    public UserInfo? DecliningUser { get; init; }
}

/// <summary>
/// Base type for shared channel invite events.
/// </summary>
/// <param name="Type">The type of event.</param>
public abstract record SharedChannelInviteEvent(string Type) : EventBody(Type)
{
    /// <summary>
    /// Information about the invitation.
    /// </summary>
    [JsonProperty("invite")]
    [JsonPropertyName("invite")]
    public required SlackInvite Invite { get; init; }

    /// <summary>
    /// The email address of the recipient.
    /// </summary>
    [JsonProperty("recipient_email")]
    [JsonPropertyName("recipient_user_id")]
    public required string RecipientEmail { get; init; }

    /// <summary>
    /// The Slack user Id of the recipient.
    /// </summary>
    [JsonProperty("recipient_user_id")]
    [JsonPropertyName("recipient_user_id")]
    public required string RecipientUserId { get; init; }

    /// <summary>
    /// The channel to invite to.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public required ConversationInfoItem Channel { get; init; }

    /// <summary>
    /// The teams in the channel.
    /// </summary>
    [JsonProperty("teams_in_channel")]
    [JsonPropertyName("teams_in_channel")]
    public IReadOnlyList<TeamInfo> TeamsInChannel { get; init; } = new List<TeamInfo>();
}

/// <summary>
/// An invitation to join a Slack channel.
/// </summary>
public record SlackInvite
{
    /// <summary>
    /// The invitation Id.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The date the invitation was created.
    /// </summary>
    [JsonProperty("date_created")]
    [JsonPropertyName("date_created")]
    public required long DateCreated { get; init; }

    /// <summary>
    /// The date the invitation becomes invalid.
    /// </summary>
    [JsonProperty("date_invalid")]
    [JsonPropertyName("date_invalid")]
    public required long DateInvalid { get; init; }

    /// <summary>
    /// The team that sent the invitation.
    /// </summary>
    [JsonProperty("inviting_team")]
    [JsonPropertyName("inviting_team")]
    public required TeamInfo InvitingTeam { get; init; }

    /// <summary>
    /// The team that sent the invitation.
    /// </summary>
    [JsonProperty("inviting_user")]
    [JsonPropertyName("inviting_user")]
    public required UserInfo InvitingUser { get; init; }
}

