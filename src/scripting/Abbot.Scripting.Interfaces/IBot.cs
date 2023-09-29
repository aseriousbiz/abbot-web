using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodaTime;
using Serious.Abbot.Scripting.Utilities;

namespace Serious.Abbot.Scripting;

/// <summary>
/// It's Abbot! Provides context and a set of services and information for your bot skill.
/// </summary>
public interface IBot
{
    /// <summary>
    /// The platform-specific ID of the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    string? MessageId { get; }

    /// <summary>
    /// The message that triggered this skill, if it was triggered by a message or a reaction to a message.
    /// </summary>
    IMessage? Message { get; }

    /// <summary>
    /// The URL to the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    Uri? MessageUrl { get; }

    /// <summary>
    /// The exact command text provided to the skill, without argument extraction.
    /// </summary>
    string CommandText { get; }

    /// <summary>
    /// Stores information specific to your bot skill.
    /// </summary>
    IBrain Brain { get; }

    /// <summary>
    /// Retrieves secrets needed by your skill such as API tokens, etc. Secrets are set in the Skill Editor.
    /// </summary>
    ISecrets Secrets { get; }

    /// <summary>
    /// Used to reply with a message to open a ticket.
    /// </summary>
    ITicketsClient Tickets { get; }

    /// <summary>
    /// Used to manage Slack conversations.
    /// </summary>
    IRoomsClient Rooms { get; }

    /// <summary>
    /// Used to manage the set of metadata fields for your organization.
    /// </summary>
    IMetadataClient Metadata { get; }

    /// <summary>
    /// Used to manage customers.
    /// </summary>
    ICustomersClient Customers { get; }

    /// <summary>
    /// Used to manage tasks.
    /// </summary>
    ITasksClient Tasks { get; }

    /// <summary>
    /// Used to retrieve information about Slack users.
    /// </summary>
    IUsersClient Users { get; }

    /// <summary>
    /// Sends a reply to the chat.
    /// </summary>
    /// <param name="text">The reply message.</param>
    Task ReplyAsync(string text);

    /// <summary>
    /// Sends a private message to the user that called the skill (Slack Only).
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="directMessage">If <c>true</c>, the reply is sent as a direct message</param>
    Task ReplyAsync(string text, bool directMessage);

    /// <summary>
    /// Sends a message to the user or room specified by the <see cref="MessageOptions"/> (Slack Only).
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyAsync(string text, MessageOptions options);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="color">The color to use for the sidebar (Slack Only) in hex (ex. #3AA3E3).</param>
    Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        string color);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        MessageOptions options);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="color">The color to use for the sidebar (Slack Only) in hex (ex. #3AA3E3).</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        string? color,
        MessageOptions? options);

    /// <summary>
    /// Sends a reply along with an image attachment. The image can be a URL to an image or a base64 encoded image.
    /// </summary>
    /// <param name="image">Either the URL to an image or the base64 encoded image.</param>
    /// <param name="text">The reply message.</param>
    /// <param name="title">(optional) A title to render for the image.</param>
    /// <param name="titleUrl">(optional) If specified, makes the title a link to this URL. Ignored if title is not set or if image is a base64 encoded image.</param>
    /// <param name="color">The color to use for the sidebar in hex (ex. #3AA3E3). Ignored if <paramref name="image"/> is a base64 encoded image.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyWithImageAsync(
        string image,
        string? text = null,
        string? title = null,
        Uri? titleUrl = null,
        string? color = null,
        MessageOptions? options = null);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options);

    /// <summary>
    /// Replies with the collection as a nicely formatted text table.
    /// </summary>
    /// <remarks>
    /// Uses the properties of <typeparamref name="T"/> for the columns. Each item in <paramref name="items"/>
    /// is a row.
    /// </remarks>
    /// <param name="items">The items to print out</param>
    Task ReplyTableAsync<T>(IEnumerable<T> items);

    /// <summary>
    /// Replies with the collection as a nicely formatted text table.
    /// </summary>
    /// <remarks>
    /// Uses the properties of <typeparamref name="T"/> for the columns. Each item in <paramref name="items"/>
    /// is a row.
    /// </remarks>
    /// <param name="items">The items to print out</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyTableAsync<T>(IEnumerable<T> items, MessageOptions? options);

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="delayInSeconds">How long to wait before the reply shows up.</param>
    Task ReplyLaterAsync(string text, long delayInSeconds);

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="delayInSeconds">How long to wait before the reply shows up.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyLaterAsync(string text, long delayInSeconds, MessageOptions? options);

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="timeSpan">How long to wait before the reply shows up.</param>
    Task ReplyLaterAsync(string text, TimeSpan timeSpan);

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="timeSpan">How long to wait before the reply shows up.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    Task ReplyLaterAsync(string text, TimeSpan timeSpan, MessageOptions? options);

    /// <summary>
    /// The platform specific identifier for the bot. For example, in Slack this is the Slack User Id.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The name of the Bot. Typically Abbot, but the bot can be renamed in your chat platform settings.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The ID of the team or organization on the platform. For example, the Slack team id.
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// The name of the skill.
    /// </summary>
    string SkillName { get; }

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// </summary>
    Uri SkillUrl { get; }

    /// <summary>
    /// The room (or channel) this skill is responding to.
    /// </summary>
    IRoom Room { get; }

    /// <summary>
    /// The customer the room this skill is responding to belongs to.
    /// </summary>
    CustomerInfo? Customer { get; }

    /// <summary>
    /// The arguments supplied to the skill. Does not include the skill name.
    /// </summary>
    IArguments Arguments { get; }

    /// <summary>
    /// The user that invoked the skill.
    /// </summary>
    IChatUser From { get; }

    /// <summary>
    /// The current <see cref="IConversation"/>.
    /// If the skill was invoked by a message within a conversation, or by a signal raised within a conversation, this value will be non-<c>null</c>.
    /// If this skill was not invoked as part of a conversation, this value will be <c>null</c>.
    /// </summary>
    IConversation? Conversation { get; }

    /// <summary>
    /// Slack. Always Slack.
    /// </summary>
    PlatformType PlatformType => PlatformType.Slack;

    /// <summary>
    /// The mentioned users (if any).
    /// </summary>
    IReadOnlyList<IChatUser> Mentions { get; }

    /// <summary>
    /// A convenience service for making HTTP requests.
    /// </summary>
    IBotHttpClient Http { get; }

    /// <summary>
    /// If <see cref="IsRequest"/> is true, then the skill is responding to an HTTP trigger request
    /// (instead of a chat message) and this property is populated with the incoming request information.
    /// </summary>
    IHttpTriggerEvent Request { get; }

    /// <summary>
    /// Sets properties of the HTTP response when the skill is called by an HTTP trigger request. Properties may
    /// only be set when <see cref="IsRequest"/> is true.
    /// </summary>
    IHttpTriggerResponse Response { get; }

    /// <summary>
    /// If true, the skill is responding to an HTTP trigger request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    bool IsRequest { get; }

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    bool IsInteraction { get; }

    /// <summary>
    /// If true, the skill is responding to a chat message.
    /// </summary>
    bool IsChat { get; }

    /// <summary>
    /// If true, the skill is being called by a playbook
    /// </summary>
    bool IsPlaybook { get; }

    /// <summary>
    /// When called by a playbook (aka <see cref="IsPlaybook"/> is <c>true</c>), this provides outputs that can be
    /// consumed by the next step of the playbook.
    /// </summary>
    IDictionary<string, object?> Outputs { get; }

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    bool IsPatternMatch { get; }

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called. Otherwise this is null.
    /// </summary>
    IPattern? Pattern { get; }

    /// <summary>
    /// The system timezone of the bot.
    /// </summary>
    DateTimeZone TimeZone { get; }

    /// <summary>
    /// Gets information about the version of Abbot the skill is running on.
    /// </summary>
    IVersionInfo VersionInfo { get; }

    /// <summary>
    /// A useful grab bag of utility methods for C# skill authors.
    /// </summary>
    IUtilities Utilities { get; }

    /// <summary>
    /// Raises a signal from the skill with the specified name and arguments.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="arguments">The arguments to pass to the skills that are subscribed to this signal.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> SignalAsync(string name, string arguments);

    /// <summary>
    /// The <see cref="ISignalEvent" /> signal that this source skill is responding to, if any.
    /// </summary>
    ISignalEvent? SignalEvent { get; }

    /// <summary>
    /// Gets an <see cref="IMessageTarget"/> that sends a message to the thread in which this message was posted.
    /// If this message is a top-level message, sending to this conversation will start a new thread.
    /// </summary>
    IMessageTarget? Thread { get; }

    /// <summary>
    /// Gets a string that describes the version of the .NET runtime that the skill is running on.
    /// </summary>
    // We ban access to RuntimeInformation from skills, but it could be useful to know and isn't something we need to keep secret.
    string RuntimeDescription { get; }
}
