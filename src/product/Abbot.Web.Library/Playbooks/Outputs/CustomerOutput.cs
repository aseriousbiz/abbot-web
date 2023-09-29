using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks.Triggers;

namespace Serious.Abbot.Playbooks.Outputs;

/// <summary>
/// If a customer in our database is matched to a customer from inputs, this contains information about the customer.
/// </summary>
public record CustomerOutput : SubmittedCustomerInfo
{
    // Drop this?
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("last_activity_utc")]
    public required DateTime? LastActivityUtc { get; init; }

    [JsonPropertyName("last_activity_days")]
    public required int LastActivityDays { get; init; }

    [JsonPropertyName("channels")]
    public required IReadOnlyList<ChannelOutput> Channels { get; init; }

    public static CustomerOutput FromCustomer(Customer customer, DateTime now)
    {
        var lastActivityDays = (int)(now - (customer.LastMessageActivityUtc ?? now)).TotalDays;
        return new()
        {
            Id = customer.Id,
            Name = customer.Name,
            LastActivityUtc = customer.LastMessageActivityUtc,
            LastActivityDays = lastActivityDays,
            Segments = customer.TagAssignments.Select(a => a.Tag.Name).ToArray(),
            Channels = customer.Rooms.Select(r => new ChannelOutput
            {
                Id = r.PlatformRoomId,
                Name = r.Name
            }).ToArray(),
        };
    }
}

