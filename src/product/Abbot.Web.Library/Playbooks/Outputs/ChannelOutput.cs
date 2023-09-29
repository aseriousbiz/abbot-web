using System.Text.Json.Serialization;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks.Outputs;

public record ChannelOutput
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("customer")]
    public CustomerOutput? Customer { get; init; }

    public static ChannelOutput FromRoom(Room room) => FromRoom(room, customer: null);

    public static ChannelOutput FromRoom(Room room, CustomerOutput? customer) => new()
    {
        Id = room.PlatformRoomId,
        Name = room.Name,
        Customer = customer,
    };
}
