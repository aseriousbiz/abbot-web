using System.Diagnostics.CodeAnalysis;

namespace Serious.Abbot.Messages;

/// <summary>
/// Response message when calling the reply controller to send a message to chat.
/// <param name="Success">
/// Whether or not posting the message was successful.
/// </param>
/// <param name="Message">
/// The response message. An error message if Success is false.
/// </param>
/// <param name="MessageId">
/// The id of the message that was posted.
/// </param>
/// </summary>
public record ProactiveBotMessageResponse(
    [property: MemberNotNull("MessageId")] bool Success,
    string Message = "",
    string? MessageId = null)
{
    public static readonly ProactiveBotMessageResponse Empty = new(false);
}
