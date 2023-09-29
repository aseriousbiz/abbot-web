using Serious.Cryptography;

namespace Serious.Slack.BotFramework.Model;

/// <summary>
/// The Message to create or update in Slack.
/// </summary>
/// <param name="ApiToken">The Slack Api token.</param>
/// <param name="Message">The Message.</param>
/// <param name="ResponseUrl">Some interaction payloads (such as ephemeral messages) provide this in order to modify a message.</param>
public record MessageChannelData(SecretString ApiToken, MessageRequest Message, Uri? ResponseUrl)
{
    /// <summary>
    /// If this message should be an ephemeral message, this is set to the user id of the user to send the message to.
    /// </summary>
    public string? EphemeralUser { get; init; }
};

/// <summary>
/// Channel data for deleting a message.
/// </summary>
/// <param name="ApiToken">The Slack Api token.</param>
/// <param name="ResponseUrl">Post to this to delete or modify a message.</param>
public record DeleteChannelData(SecretString ApiToken, Uri? ResponseUrl);
