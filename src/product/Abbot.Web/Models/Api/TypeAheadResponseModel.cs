using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Slack;

namespace Serious.Abbot.Models.Api;

public record TypeAheadResponseModel(
    [property: JsonProperty("value")]
    [property: JsonPropertyName("value")]
    string Value,

    [property: JsonProperty("label")]
    [property: JsonPropertyName("label")]
    string Label,

    [property: JsonProperty("image")]
    [property: JsonPropertyName("image")]
    string? Image = null)
{
    public static TypeAheadResponseModel Create(Room room) => new(room.PlatformRoomId, room.Name ?? string.Empty);

    public static TypeAheadResponseModel Create(CustomerTag segment) => new(segment.Name, segment.Name);

    public static TypeAheadResponseModel Create(Customer customer) => new($"{customer.Id}", customer.Name);

    public static TypeAheadResponseModel Create(Member member) => new(member.User.PlatformUserId, member.DisplayName, member.User.Avatar);

    public static TypeAheadResponseModel Create(Skill skill) => new(skill.Name, skill.Name);

    public static TypeAheadResponseModel Create(Emoji emoji) => emoji switch
    {
        UnicodeEmoji unicodeEmoji => new(unicodeEmoji.CanonicalName ?? unicodeEmoji.Name, $"{unicodeEmoji.Emoji} {unicodeEmoji.Name}"),
        CustomEmoji customEmoji => new(customEmoji.Name, customEmoji.Name, customEmoji.ImageUrl.ToString()),
        _ => new(emoji.Name, emoji.Name),
    };

}
