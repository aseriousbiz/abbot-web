using System.Text.Json.Serialization;
using Serious.Abbot.Entities;
using Serious.Slack;

namespace Serious.Abbot.Playbooks.Outputs;

public record ActorOutput
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    public static ActorOutput FromMember(Member member) => new()
    {
        Id = member.User.PlatformUserId,
        Name = member.DisplayName,
    };

    public static ActorOutput? FromUserInfo(UserInfo? user) => user is not null
        ? new()
        {
            Id = user.Id,
            Name = user.Profile.DisplayName ?? "",
        }
        : null;
}
