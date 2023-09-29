using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack;

namespace Serious.Abbot.AI;


public record SanitizedSourceMessage(
    SourceMessage SourceMessage,
    IReadOnlyDictionary<string, SecretString> Replacements)
{
    public static readonly SanitizedSourceMessage Empty =
        new(SourceMessage.Empty, new Dictionary<string, SecretString>());

    public static SanitizedSourceMessage? FromMessagePostedEvent(
        MessagePostedEvent messagePostedEvent,
        IReadOnlyDictionary<string, SecretString> existingReplacements)
    {
        var messagePostedMetadata = messagePostedEvent.DeserializeMetadata();
        if (messagePostedMetadata?.Text is null || messagePostedEvent.MessageId is null)
        {
            return null;
        }

        var sanitizedText = SensitiveDataSanitizer.Sanitize(
            messagePostedMetadata.Text,
            messagePostedMetadata.SensitiveValues,
            existingReplacements: existingReplacements);

        var sourceUser = SourceUser.FromMessagePostedEvent(messagePostedEvent);
        var sourceMessage = new SourceMessage(
            sanitizedText.Message,
            sourceUser,
            SlackTimestamp.Parse(messagePostedEvent.MessageId),
            CompletionInfo.FromAIResult(messagePostedMetadata.SummarizationResult),
            CompletionInfo.FromAIResult(messagePostedMetadata.ConversationMatchAIResult));

        return new(sourceMessage, sanitizedText.Replacements);
    }
}
