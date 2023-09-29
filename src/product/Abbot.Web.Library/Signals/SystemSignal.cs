using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Serious.Abbot.AI;

namespace Serious.Abbot.Signals;

/// <summary>
/// Represents a "system" signal, which is a signal that is raised by Abbot directly in response to certain events.
/// </summary>
public class SystemSignal
{
    public static readonly string Prefix = "system:";

    /// <summary>
    /// List all system signals.
    /// </summary>
    public static IReadOnlyList<SystemSignal> All { get; }

    // Executes after field initializers
    static SystemSignal()
    {
        All = typeof(SystemSignal)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetValue(null))
            .OfType<SystemSignal>()
            .ToList();

        Debug.Assert(All.Count > 0);
    }

    /// <summary>
    /// A signal that can be raised in a room via a staff tool.
    /// </summary>
    public static readonly SystemSignal StaffTestSignal = new(
        "system:staff:test",
        "A test signal staff can raise.",
        staffOnly: true);

    /// <summary>
    /// A signal raised when Abbot is added to a room.
    /// </summary>
    public static readonly SystemSignal AbbotAddedToRoom = new(
        name: "system:abbot:added",
        description: "Abbot added to a room.");

    /// <summary>
    /// A signal raised whenever a conversation is started.
    /// </summary>
    public static readonly SystemSignal ConversationStartedSignal = new(
        "system:conversation:started",
        "A conversation was started.");

    /// <summary>
    /// A signal raised when a conversation is overdue for a response.
    /// </summary>
    public static readonly SystemSignal ConversationOverdueSignal = new(
        name: "system:conversation:overdue",
        description: "Conversation is overdue for a response.");

    /// <summary>
    /// Creates a system signal from a category.
    /// </summary>
    /// <param name="category">A <see cref="Category"/>.</param>
    /// <returns>Returns a <see cref="SystemSignal"/> using the category name.</returns>
    public static SystemSignal CreateSignalFromCategory(Category category) => new(
        $"system:conversation:category:{category.Name}",
        "Signal raised based on the OpenAI classifier.",
        fromAI: true);

    /// <summary>
    /// A signal raised when a conversation is believed to have negative sentiment.
    /// </summary>
    public static readonly SystemSignal ConversationCategorySentimentSignal = new(
        CreateSignalFromCategory(new("sentiment", "")).Name,
        "Conversation sentiment. The argument is the sentiment, e.g. negative.",
        fromAI: true);

    /// <summary>
    /// A signal raised when a conversation state change is suggested.
    /// </summary>
    public static readonly SystemSignal ConversationCategoryStateSignal = new(
        CreateSignalFromCategory(new("state", "")).Name,
        "Conversation state could change. The argument is a suggested state, e.g. closed or snoozed.",
        fromAI: true);

    /// <summary>
    /// A signal raised when a conversation state change is suggested.
    /// </summary>
    public static readonly SystemSignal ConversationCategoryTopicSignal = new(
        CreateSignalFromCategory(new("topic", "")).Name,
        "Conversation topics. The argument is a conversation topic, e.g. documentation, outage, or bug.",
        fromAI: true);

    /// <summary>
    /// A signal raised when a reaction is added or removed from a message.
    /// </summary>
    public static readonly SystemSignal ReactionAddedSignal = new(
        "system:reaction:added",
        "A reaction was added to a message. The arguments is the reaction name. Check Bot.MessageId etc for information about the message that was reacted to.");

    /// <summary>
    /// A signal raised when a conversation is linked to a ticket.
    /// </summary>
    public static readonly SystemSignal TicketLinkedSignal = new(
        "system:conversation:linked:ticket",
        "A ticket was linked to a conversation. The arguments is a JSON serialization of the link info.");

    /// <summary>
    /// A signal raised when a linked ticket's state changed.
    /// </summary>
    public static readonly SystemSignal TicketStateChangedSignal = new(
       "system:conversation:linked:ticket:state",
        "A linked ticket's state changed. The arguments are a JSON serialization of the ticket state.");

    /// <summary>
    /// The name of the system signal, which must start with 'system:'
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A brief description of the system signal, for showing in UI.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The signal is expected to be raised from AI.
    /// </summary>
    public bool FromAI { get; }

    /// <summary>
    /// If <c>true</c>, this signal will shown only in Staff Mode.
    /// Users can still subscribe skills to this signal if they know it's name.
    /// </summary>
    public bool StaffOnly { get; }

    /// <summary>
    /// Constructs a <see cref="SystemSignal"/>
    /// </summary>
    /// <param name="name">The name of the system signal, which must start with 'system:'</param>
    /// <param name="description">A brief description of the system signal, for showing in UI.</param>
    /// <param name="fromAI">The signal is raised from an AI.</param>
    /// <param name="staffOnly">Should this signal be shown only in Staff Mode?</param>
    /// <exception cref="ArgumentException">The <paramref name="name"/> does not start with 'system:'</exception>
    public SystemSignal(string name, string description, bool fromAI = false, bool staffOnly = false)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("System signal names must start with 'system:'", nameof(name));
        }

        Name = name;
        Description = description;
        FromAI = fromAI;
        StaffOnly = staffOnly;
    }
}
