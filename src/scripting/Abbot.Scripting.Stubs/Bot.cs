using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NodaTime;
using Serious.Abbot.Scripting.Utilities;

[assembly: CLSCompliant(false)]
namespace Serious.Abbot.Scripting;

/// <summary>
/// It's Abbot! Provides a set of services and information for your bot skill.
/// </summary>
// ReSharper disable once UnusedType.Global
public class Bot : IBot
{
    /// <summary>
    /// The platform-specific ID of the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    public string? MessageId => null;

    /// <summary>
    /// Information about the message that triggered this skill, or the message that was reacted to in order to
    /// trigger this skill.
    /// </summary>
    public IMessage? Message => null;

    /// <summary>
    /// The URL to the message that triggered this skill, if it was triggered by a message.
    /// </summary>
    public Uri? MessageUrl => null;

    /// <summary>
    /// The exact message provided to the skill, without argument extraction.
    /// </summary>
    public string CommandText => string.Empty;

    /// <summary>
    /// Stores information specific to your bot skill.
    /// </summary>
    public IBrain Brain => null!;

    /// <summary>
    /// Retrieves secrets needed by your skill such as API tokens, etc. Secrets are set in the Skill Editor.
    /// </summary>
    public ISecrets Secrets => null!;

    /// <summary>
    /// Used to reply with a message to open a ticket.
    /// </summary>
    public ITicketsClient Tickets => null!;

    /// <summary>
    /// Used to manage Slack conversations.
    /// </summary>
    public IRoomsClient Rooms { get; } = null!;

    /// <summary>
    /// Used to manage the set of metadata fields for your organization.
    /// </summary>
    public IMetadataClient Metadata { get; } = null!;

    /// <summary>
    /// Used to manage customers.
    /// </summary>
    public ICustomersClient Customers { get; } = null!;

    /// <summary>
    /// Used to manage tasks.
    /// </summary>
    public ITasksClient Tasks { get; } = null!;

    /// <summary>
    /// Used to retrieve information about Slack users.
    /// </summary>
    public IUsersClient Users { get; } = null!;

    /// <summary>
    /// Sends a reply to the chat.
    /// </summary>
    /// <param name="text">The reply message.</param>
    public Task ReplyAsync(string text) => throw new NotImplementedException();

    /// <summary>
    /// Sends a private message to the user that called the skill (Slack Only).
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="directMessage">If <c>true</c>, the reply is sent as a direct message</param>
    public Task ReplyAsync(string text, bool directMessage) => throw new NotImplementedException();

    /// <summary>
    /// Sends a private message to the user that called the skill (Slack Only).
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyAsync(string text, MessageOptions options) => throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl, string? title) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="color">The color to use for the sidebar (Slack Only) in hex (ex. #3AA3E3).</param>
    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl, string? title,
        string color) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="imageUrl">(optional) An image to render before the set of buttons.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, string? buttonsLabel, Uri? imageUrl, string? title,
        MessageOptions options) =>
        throw new NotImplementedException();

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
    public Task ReplyWithButtonsAsync(
        string text,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        Uri? imageUrl,
        string? title,
        string? color,
        MessageOptions? options) => throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with an image attachment. The image can be a URL to an image or a base64 encoded image.
    /// </summary>
    /// <param name="image">Either the URL to an image or the base64 encoded image.</param>
    /// <param name="text">The reply message.</param>
    /// <param name="title">(optional) A title to render for the image.</param>
    /// <param name="titleUrl">(optional) If specified, makes the title a link to this URL. Ignored if title is not set.</param>
    /// <param name="color">The color to use for the sidebar (Slack Only) in hex (ex. #3AA3E3).</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyWithImageAsync(
        string image,
        string? text = null,
        string? title = null,
        Uri? titleUrl = null,
        string? color = null,
        MessageOptions? options = null) => throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options = null) => throw new NotImplementedException();

    /// <summary>
    /// Replies with the collection as a nicely formatted text table.
    /// </summary>
    /// <remarks>
    /// Uses the properties of <typeparamref name="T"/> for the columns. Each item in <paramref name="items"/>
    /// is a row.
    /// </remarks>
    /// <param name="items">The items to print out</param>
    public Task ReplyTableAsync<T>(IEnumerable<T> items) =>
        throw new NotImplementedException();

    /// <summary>
    /// Replies with the collection as a nicely formatted text table.
    /// </summary>
    /// <remarks>
    /// Uses the properties of <typeparamref name="T"/> for the columns. Each item in <paramref name="items"/>
    /// is a row.
    /// </remarks>
    /// <param name="items">The items to print out</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyTableAsync<T>(IEnumerable<T> items, MessageOptions? options = null) => throw new NotImplementedException();

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="delayInSeconds">How long to wait before the reply shows up.</param>
    public Task ReplyLaterAsync(string text, long delayInSeconds) => throw new NotImplementedException();

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="delayInSeconds">How long to wait before the reply shows up.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyLaterAsync(string text, long delayInSeconds, MessageOptions? options) => throw new NotImplementedException();

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="timeSpan">How long to wait before the reply shows up.</param>
    public Task ReplyLaterAsync(string text, TimeSpan timeSpan) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sends the reply later.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="timeSpan">How long to wait before the reply shows up.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    public Task ReplyLaterAsync(string text, TimeSpan timeSpan, MessageOptions? options = null) => throw new NotImplementedException();

    /// <summary>
    /// Raises a signal from the skill with the specified name and arguments.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="arguments">The arguments to pass to the skills that are subscribed to this signal.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    public Task<IResult> SignalAsync(string name, string arguments) => throw new NotImplementedException();

    /// <summary>
    /// The platform specific identifier for the bot. For example, in Slack this is the Slack User Id.
    /// </summary>
    public string Id => null!;

    /// <summary>
    /// The name of the Bot. Typically Abbot, but the bot can be renamed in your chat platform settings.
    /// </summary>
    public string Name => null!;

    /// <summary>
    /// The ID of the team or organization on the platform. For example, the Slack team id.
    /// </summary>
    public string PlatformId => null!;

    /// <summary>
    /// The name of the skill.
    /// </summary>
    public string SkillName => null!;

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// </summary>
    public Uri SkillUrl => null!;

    /// <summary>
    /// The room (or channel) name this skill is responding to.
    /// </summary>
    public IRoom Room => null!;

    /// <summary>
    /// The customer the room this skill is responding to belongs to.
    /// </summary>
    public CustomerInfo? Customer => null;

    /// <summary>
    /// The arguments supplied to the skill. Does not include the skill name.
    /// </summary>
    public IArguments Arguments => null!;

    /// <summary>
    /// The user that invoked the skill.
    /// </summary>
    public IChatUser From => null!;

    /// <summary>
    /// The current <see cref="IConversation"/>.
    /// If the skill was invoked by a message within a conversation, or by a signal raised within a conversation, this value will be non-<c>null</c>.
    /// If this skill was not invoked as part of a conversation, this value will be <c>null</c>.
    /// </summary>
    public IConversation? Conversation => null;

    /// <summary>
    /// The platform the skill is running on such as Slack or Teams.
    /// </summary>
    public PlatformType PlatformType => PlatformType.Slack;

    /// <summary>
    /// The mentioned users (if any).
    /// </summary>
    public IReadOnlyList<IChatUser> Mentions => null!;

    /// <summary>
    /// A convenience service for making HTTP requests.
    /// </summary>
    public IBotHttpClient Http => null!;

    /// <summary>
    /// If <see cref="IsRequest"/> is true, then the skill is responding to an HTTP trigger request
    /// (instead of a chat message) and this property is populated with the incoming request information.
    /// </summary>
    public IHttpTriggerEvent Request => null!;

    /// <summary>
    /// Sets properties of the HTTP response when the skill is called by an HTTP trigger request. Properties may
    /// only be set when <see cref="IsRequest"/> is true.
    /// </summary>
    public IHttpTriggerResponse Response => null!;

    /// <summary>
    /// If true, the skill is responding to an HTTP trigger request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    public bool IsRequest => false;

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    public bool IsInteraction => false;

    /// <summary>
    /// If true, the skill is responding to a chat message.
    /// </summary>
    public bool IsChat => false;

    /// <inheritdoc />
    public bool IsPlaybook => false;

    /// <inheritdoc />
    public IDictionary<string, object?> Outputs => null!;

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    public bool IsPatternMatch => false;

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called.
    /// </summary>
    public IPattern? Pattern => null;

    /// <summary>
    /// The system timezone of the bot.
    /// </summary>
    public DateTimeZone TimeZone => null!;

    /// <summary>
    /// Gets information about the version of Abbot the skill is running on.
    /// </summary>
    public IVersionInfo VersionInfo => null!;

    /// <summary>
    /// A useful grab bag of utility methods for C# skill authors.
    /// </summary>
    public IUtilities Utilities => null!;

    /// <summary>
    /// The <see cref="ISignalEvent" /> signal that this source skill is responding to, if any.
    /// </summary>
    public ISignalEvent? SignalEvent { get; }

    /// <summary>
    /// Gets an <see cref="IMessageTarget"/> that sends a message to the thread in which this message was posted.
    /// If this message is a top-level message, sending to this conversation will start a new thread.
    /// </summary>
    public IMessageTarget? Thread => null!;

    /// <inheritdoc />
    public string RuntimeDescription => RuntimeInformation.FrameworkDescription;
}
