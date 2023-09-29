namespace Serious.Abbot.Messages;

/// <summary>
/// A request to show an ephemeral message with a Create Ticket prompt. The prompt contains a button for each
/// enabled ticketing integration.
/// </summary>
/// <remarks>
/// It may seem <paramref cref="MessageId"/> is redundant with the message Id in
/// <paramref cref="ConversationIdentifier"/>, but the outer one determines where the Ticket Prompt message is sent.
/// The other identifies the conversation the ticket applies to. Often, these are in the same thread, but doesn't
/// have to be.
/// </remarks>
/// <param name="User">The slack user id to show the buttons to as an ephemeral message</param>
/// <param name="MessageId">The Id of the message that triggered this skill, if any. This is used to determine whether the prompt should reply in a thread or top-level.</param>
/// <param name="ConversationIdentifier">Identifies the conversation that should be imported into the ticket if a ticket is created.</param>
public record TicketPromptRequest(string User, string? MessageId, ConversationIdentifier ConversationIdentifier);
