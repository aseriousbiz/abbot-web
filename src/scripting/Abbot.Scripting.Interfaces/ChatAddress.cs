namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an address to which messages can be sent.
/// </summary>
/// <param name="Type">A <see cref="ChatAddressType"/> indicating the type of the conversation referenced by the <paramref name="Id"/></param>
/// <param name="Id">The ID of a conversation (User/Room) to send to.</param>
/// <param name="ThreadId">An optional ID indicating a thread within the User/Room conversation indicated by the <paramref name="Id"/></param>
/// <param name="MessageId">An optional ID indicating a message within the User/Room conversation indicated by the <paramref name="Id"/></param>
/// <param name="EphemeralUser">An optional slack user id indicating a user to send an ephemeral message to. This is only valid if <see cref="ChatAddressType"/> is <see cref="ChatAddressType.Room"/>.</param>
public readonly record struct ChatAddress(
    ChatAddressType Type,
    string Id,
    string? ThreadId = null,
    string? MessageId = null,
    string? EphemeralUser = null)
{
    /// <summary>
    /// Produces a string representation of the <see cref="ChatAddress"/>.
    /// </summary>
    /// <remarks>We don't parse this anywhere. It's just for display.</remarks>
    public override string ToString() => AppendEphemeralId(AppendThreadId(AppendMessageId($"{Type}/{Id}")));

    string AppendMessageId(string text) => MessageId is { Length: > 0 }
        ? $"{text}(MSG:{MessageId})"
        : text;
    string AppendThreadId(string text) => ThreadId is { Length: > 0 }
        ? $"{text}(THREAD:{ThreadId})"
        : text;

    string AppendEphemeralId(string text) => EphemeralUser is { Length: > 0 }
        ? $"{text}(EPHEMERAL:{EphemeralUser})"
        : text;
}
