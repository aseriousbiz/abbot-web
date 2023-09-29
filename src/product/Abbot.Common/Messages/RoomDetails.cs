using System;
using System.Collections.Generic;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

public record RoomDetails(
    string Id,
    string? Name,
    bool? BotIsMember,
    bool ConversationTrackingEnabled,
    bool Archived,
    CustomerInfo? Customer,
    IResponseSettings ResponseSettings,
    IResponseSettings DefaultResponseSettings,
    IReadOnlyDictionary<string, string> Metadata) : PlatformRoom(Id, Name), IRoomDetails;

public record ResponseSettings(
    Threshold<TimeSpan> ResponseTime,
    IReadOnlyList<IChatUser> FirstResponders,
    IReadOnlyList<IChatUser> EscalationResponders) : IResponseSettings;
