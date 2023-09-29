using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Outputs;

public record ConversationOutput
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("state")]
    public required ConversationState? State { get; init; }

    [JsonPropertyName("message")]
    public required MessageOutput? Message { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyList<string> Tags { get; init; }

    public static ConversationOutput FromConversation(Conversation conversation, IEnumerable<AI.Category>? categories) => new()
    {
        Id = conversation.Id,
        Title = conversation.Title,
        State = conversation.State,
        Message = new()
        {
            Channel = ChannelOutput.FromRoom(conversation.Room),
            Timestamp = conversation.FirstMessageId,
            Url = conversation.GetFirstMessageUrl(),
        },
        Tags = categories?.Select(c => c.ToString()).ToArray()
               ?? conversation.Tags.Select(t => t.Tag.Name).ToArray(),
    };
}
