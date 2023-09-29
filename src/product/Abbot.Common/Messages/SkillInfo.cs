using System;
using System.Collections.Generic;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Provides context for the skills themselves.
/// </summary>
/// <remarks>
/// This is a property of <see cref="SkillMessage" /> that is sent to skill runners in a serialized format.
/// </remarks>
public class SkillInfo
{
    /// <summary>
    /// The platform-specific ID of the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    [Obsolete("Use Message instead")]
    public string? MessageId { get; init; }

    /// <summary>
    /// The URL of the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    [Obsolete("Use Message instead")]
    public Uri? MessageUrl { get; init; }

    /// <summary>
    /// Information about the message that triggered this skill, or the message that was reacted to in order to
    /// trigger this skill.
    /// </summary>
    public SourceMessageInfo? Message { get; init; }

    /// <summary>
    /// The platform-specific ID of the thread that that the message that triggered this skill is in, if it was
    /// triggered by a message. If <see cref="MessageId"/> is not null and this is null, then the thread Id for a
    /// response is the <see cref="MessageId"/>.
    /// </summary>
    [Obsolete("Use Message instead")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// The chat platform type for which this skill runs
    /// </summary>
    [Obsolete("We only support Slack now")]
    public PlatformType PlatformType { get; init; } = PlatformType.Slack;

    /// <summary>
    /// The platform on which the message was received, which MAY differ from the platform type of the organization.
    /// </summary>
    [Obsolete("We only support Slack now")]
    public PlatformType MessagePlatformType { get; init; } = PlatformType.Slack;

    /// <summary>
    /// The ID of the team or organization on the platform. For example, the Slack team id.
    /// </summary>
    public string PlatformId { get; init; } = null!;

    /// <summary>
    /// The name of the skill.
    /// </summary>
    public string SkillName { get; init; } = null!;

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// </summary>
    public Uri SkillUrl { get; init; } = null!;

    /// <summary>
    /// The room (or channel) name this skill is responding to.
    /// </summary>
    public PlatformRoom Room { get; init; } = null!;

    /// <summary>
    /// The customer of the room, if any.
    /// </summary>
    public CustomerInfo? Customer { get; init; }

    /// <summary>
    /// The raw arguments passed to the skill.
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// A parsed tokenized set of arguments.
    /// </summary>
    public IReadOnlyList<Argument> TokenizedArguments { get; init; } = Array.Empty<Argument>();

    /// <summary>
    /// The <see cref="IPattern"/> that was matched (if any) for this message.
    /// </summary>
    public PatternMessage? Pattern { get; init; }

    /// <summary>
    /// The user that is calling the skill.
    /// </summary>
    public PlatformUser From { get; init; } = null!;

    /// <summary>
    /// The bot user.
    /// </summary>
    public PlatformUser Bot { get; init; } = null!;

    /// <summary>
    /// The mentioned users.
    /// </summary>
    public IReadOnlyList<PlatformUser> Mentions { get; init; } = Array.Empty<PlatformUser>();

    /// <summary>
    /// If called by an HTTP trigger, this contains information about the request.
    /// </summary>
    public HttpTriggerRequest? Request { get; init; }

    /// <summary>
    /// If true, the skill was triggered by an HTTP request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    public bool IsRequest { get; init; }

    /// <summary>
    /// If true, the skill was triggered by chat.
    /// </summary>
    public bool IsChat { get; init; }

    /// <summary>
    /// If true, the skill was initiated by a playbook.
    /// </summary>
    public bool IsPlaybook { get; init; }

    /// <summary>
    /// If true, the skill was triggered by a signal.
    /// </summary>
    public bool IsSignal { get; init; }

    /// <summary>
    /// Whether or not this message represents a user interaction with a UI element in chat such as clicking
    /// on a <see cref="Button" /> button. In that case, the <see cref="SkillInfo.Arguments" /> contain the
    /// value of the button.
    /// </summary>
    public bool IsInteraction { get; init; }

    /// <summary>
    /// The raw text that was used to invoke the skill.
    /// </summary>
    public string CommandText { get; init; } = string.Empty;
}
