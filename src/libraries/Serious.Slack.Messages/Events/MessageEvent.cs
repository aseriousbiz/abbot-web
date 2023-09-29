using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Events;

/// <summary>
/// Represents a Slack Message event https://api.slack.com/events/message.
/// </summary>
[Element("message")]
public record MessageEvent : MessageEventBody
{
    /// <summary>
    /// Constructs a <see cref="MessageEvent"/> with the <c>type</c> of <c>message</c>.
    /// </summary>
    public MessageEvent()
    {
    }

    /// <summary>
    /// Constructs a <see cref="MessageEvent"/> with the <c>type</c> of <c>message</c>.
    /// </summary>
    /// <param name="type">The <c>type</c> of message.</param>
    protected MessageEvent(string type) : base(type)
    {
    }

    /// <summary>
    /// The text of the message.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// I do not know what this is, but I see it in the events.
    /// </summary>
    [JsonProperty("client_message_id")]
    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; init; }

    /// <summary>
    /// The ID of the channel where this event occurred. I think this is deprecated.
    /// </summary>
    [JsonProperty("channel_id")]
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }

    /// <summary>
    /// If <see cref="MessageEventBody.SubType"/> is <c>bot_message</c>, then this is the bot's ID (ex. B0123456789).
    /// </summary>
    [JsonProperty("bot_id")]
    [JsonPropertyName("bot_id")]
    public string? BotId { get; init; }

    /// <summary>
    /// If <see cref="MessageEventBody.SubType"/> is <c>bot_message</c>, then this is the bot's user name.
    /// </summary>
    [JsonProperty("username")]
    [JsonPropertyName("username")]
    public string? UserName { get; init; }

    /// <summary>
    /// If the message has been edited after posting this property is not null and includes the user ID of
    /// the editor, and the timestamp the edit happened. The original text of the message is not available.
    /// </summary>
    [JsonProperty("edited")]
    [JsonPropertyName("edited")]
    public EditInfo? Edited { get; init; }

    /// <summary>
    /// The unique Id for the Team or Workspace the message originated in.
    /// This can be <c>null</c> for some message subtypes, like 'file_share'.
    /// However, it is only <c>null</c> if the message came from the same team as the event.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public string? Team { get; init; }

    /// <summary>
    /// The Slack timestamp of the thread's root message, if any.
    /// </summary>
    [JsonProperty("thread_ts")]
    [JsonPropertyName("thread_ts")]
    public string? ThreadTimestamp { get; init; }

    /// <summary>
    /// Identifies the Enterprise workspace the message originates from
    /// </summary>
    [JsonProperty("source_team")]
    [JsonPropertyName("source_team")]
    public string? SourceTeam { get; init; }

    /// <summary>
    /// The set of blocks contained in the message.
    /// </summary>
    [JsonProperty("blocks")]
    [JsonPropertyName("blocks")]
    public IReadOnlyList<ILayoutBlock> Blocks { get; init; } = Array.Empty<ILayoutBlock>();
}

/// <summary>
/// Represents the information about a deleted message. This is raised when someone in our own Workspace deletes a
/// message.
/// </summary>
[Element("message", Discriminator = "subtype", DiscriminatorValue = "message_deleted")]
public record MessageDeletedEvent : MessageEventBody
{
    /// <summary>
    /// If <c>true</c>, then this message is hidden (probably deleted).
    /// </summary>
    [JsonProperty("hidden")]
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    /// <summary>
    /// The unique (per-channel) timestamp for the deleted message.
    /// </summary>
    [JsonProperty("deleted_ts")]
    [JsonPropertyName("deleted_ts")]
    public string DeletedTimestamp { get; init; } = null!;

    /// <summary>
    /// The deleted message.
    /// </summary>
    [JsonProperty("previous_message")]
    [JsonPropertyName("previous_message")]
    public SlackMessage PreviousMessage { get; init; } = null!;
}

/// <summary>
/// Information about a message change event.
/// </summary>
/// <remarks>
/// When a message in a shared channel is deleted by a foreign member, we receive this event, not a message_deleted
/// event.
/// </remarks>
[Element("message", Discriminator = "subtype", DiscriminatorValue = "message_changed")]
public record MessageChangedEvent : MessageEventBody
{
    /// <summary>
    /// If <c>true</c>, then this message is hidden (probably deleted).
    /// </summary>
    [JsonProperty("hidden")]
    [JsonPropertyName("hidden")]
    public bool Hidden { get; init; }

    /// <summary>
    /// The deleted message.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public SlackMessage Message { get; init; } = null!;

    /// <summary>
    /// The deleted message.
    /// </summary>
    [JsonProperty("previous_message")]
    [JsonPropertyName("previous_message")]
    public SlackMessage PreviousMessage { get; init; } = null!;
}


/// <summary>
/// Information about a channel rename event. When a shared channel is renamed, we get message with the subtype
/// <c>channel_name</c> instead of a rename event.
/// </summary>
/// <remarks>
/// When a room in a shared channel is renamed, we receive this event, not a channel_rename event.
/// </remarks>
[Element("message", Discriminator = "subtype", DiscriminatorValue = "channel_name")]
public record ChannelRenameMessageEvent : MessageEventBody
{
    /// <summary>
    /// The old name of the room.
    /// </summary>
    [JsonProperty("old_name")]
    [JsonPropertyName("old_name")]
    public string OldName { get; init; } = null!;

    /// <summary>
    /// The new name of the room
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;
}

/// <summary>
/// A base class for message events.
/// </summary>
public abstract record MessageEventBody : EventBody
{
    /// <summary>
    /// Constructs a <see cref="MessageEventBody"/> with the <c>type</c> of <c>message</c>.
    /// </summary>
    protected MessageEventBody() : base("message")
    {
    }

    /// <summary>
    /// Constructs a <see cref="MessageEvent"/> with the <c>type</c> of <c>message</c>.
    /// </summary>
    /// <param name="type">The <c>type</c> of message.</param>
    protected MessageEventBody(string type) : base(type)
    {
    }

    /// <summary>
    /// The type of the channel this message was posted in.
    /// </summary>
    [JsonProperty("channel_type")]
    [JsonPropertyName("channel_type")]
    public string ChannelType { get; set; } = null!;

    /// <summary>
    /// The subtype of the message. For example, <c>channel_join</c> subtype for a message when someone joins a
    /// channel or <c>bot_message</c> subtype for a message from a bot.
    /// </summary>
    [JsonProperty("subtype")]
    [JsonPropertyName("subtype")]
    public string? SubType { get; init; }

    /// <summary>
    /// The channel where this event occurred.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string? Channel { get; init; }
}

/// <summary>
/// Represents a message with file attachments.
/// </summary>
[Element("message", Discriminator = "subtype", DiscriminatorValue = "file_share")]
public sealed record FileShareMessageEvent() : MessageEvent("message")
{
    /// <summary>
    /// The set of fiels being uploaded.
    /// </summary>
    [JsonProperty("files")]
    [JsonPropertyName("files")]
    public IReadOnlyList<FileUpload> Files { get; init; } = Array.Empty<FileUpload>();

    /// <summary>
    /// ???
    /// </summary>
    [JsonProperty("upload")]
    [JsonPropertyName("upload")]
    public bool Upload { get; init; }

    /// <summary>
    /// ???
    /// </summary>
    [JsonProperty("display_as_bot")]
    [JsonPropertyName("display_as_bot")]
    public bool DisplayAsBot { get; init; }
}

/// <summary>
/// Information about a file upload. <see href="https://api.slack.com/events/message/file_share"> the docs for more info</see>.
/// </summary>
/// <remarks>
/// When a file is uploaded into a Slack Connect channel, file object properties are not immediately accessible to apps
/// listening via the Events API. Instead, the payload will contain a file object with the key-value pair
/// "file_access": "check_file_info" meaning that further action is required from your app in order to view an uploaded
/// file's metadata.
/// </remarks>
/// <param name="Id">The Id of the file</param>
/// <param name="FileAccess">A value of <c>check_file_info</c> indicates further action is required in order to view an uploaded file's metadata. AKA, another API call.</param>
/// <param name="Created">Slack timestamp when the file was created. Always 0 for files uploaded to Slack connect.</param>
/// <param name="Timestamp">Slack timestamp when the file was received. Always 0 for Events API.</param>
/// <param name="User">Id of the creator. Empty string for the Events API. Always 0 for files uploaded to Slack connect.</param>
public record FileUpload(

    [property: JsonProperty("id")]
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonProperty("file_access")]
    [property: JsonPropertyName("file_access")]
    string FileAccess,

    [property: JsonProperty("created")]
    [property: JsonPropertyName("created")]
    long Created,

    [property: JsonProperty("timestamp")]
    [property: JsonPropertyName("timestamp")]
    long Timestamp,

    [property: JsonProperty("user")]
    [property: JsonPropertyName("user")]
    string User);

/// <summary>
/// A <see cref="MessageEvent"/> where the App's bot user is mentioned.
/// </summary>
[Element("app_mention")]
public sealed record AppMentionEvent() : MessageEvent("app_mention");

/// <summary>
/// A <see cref="MessageEvent"/> with the Subtype <c>bot_message</c>.
/// </summary>
[Element("message", Discriminator = "subtype", DiscriminatorValue = "bot_message")]
public sealed record BotMessageEvent() : MessageEvent("message")
{
    /// <summary>
    /// The profile for the bot that sent the message.
    /// </summary>
    [JsonProperty("bot_profile")]
    [JsonPropertyName("bot_profile")]
    public BotProfile? BotProfile { get; init; }
}

/// <summary>
/// The profile for a Bot.
/// </summary>
/// <param name="Id">The Bot Id. This is not the Bot's User Id.</param>
/// <param name="Deleted">Whether the bot has been deleted or not.</param>
/// <param name="Name">The name of the bot or workflow.</param>
/// <param name="Updated">A Slack timestamp for when the bot was updated.</param>
/// <param name="AppId">The App Id.</param>
/// <param name="IsWorkflowBot">Whether or not the bot is a workflow bot.</param>
/// <param name="TeamId">The team Id</param>
/// <param name="Icons">The icons for the bot.</param>
public record BotProfile(

    [property: JsonProperty("id")]
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonProperty("deleted")]
    [property: JsonPropertyName("deleted")]
    bool Deleted,

    [property: JsonProperty("name")]
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonProperty("updated")]
    [property: JsonPropertyName("updated")]
    string Updated,

    [property: JsonProperty("app_id")]
    [property: JsonPropertyName("app_id")]
    string AppId,

    [property: JsonProperty("is_workflow_bot")]
    [property: JsonPropertyName("is_workflow_bot")]
    bool IsWorkflowBot,

    [property: JsonProperty("team_id")]
    [property: JsonPropertyName("team_id")]
    string TeamId,

    [property: JsonProperty("icons")]
    [property: JsonPropertyName("icons")]
    BotIcons Icons);
