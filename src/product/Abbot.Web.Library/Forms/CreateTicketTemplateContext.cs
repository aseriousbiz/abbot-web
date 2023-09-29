using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Forms;

public record CreateTicketTemplateContext(
    ConversationTemplateModel Conversation,
    RoomTemplateModel Room,
    OrganizationTemplateModel Organization,
    MemberTemplateModel Actor,
    CustomerTemplateModel? Customer = null);

public record OrganizationTemplateModel(
    int Id,
    string? Name,
    string? Domain,
    string? Avatar,
    PlatformType PlatformType,
    string? PlatformTeamId)
{
    public static OrganizationTemplateModel FromOrganization(Organization org)
        => new(org.Id, org.Name, org.Domain, org.Avatar, org.PlatformType, org.PlatformId);
}

public record ConversationTemplateModel(
    int Id,
    string Url,
    string MessageUrl,
    string? Title,
    string? PlainTextTitle,
    DateTime LastMessagePostedOn,
    MemberTemplateModel StartedBy,
    ConversationState State = ConversationState.Unknown)
{
    public static ConversationTemplateModel FromConversation(Conversation convo, Uri conversationUrl, string plainTextTitle) =>
        new(convo.Id,
            conversationUrl.ToString(),
            convo.GetFirstMessageUrl().ToString(),
            convo.Title,
            plainTextTitle,
            convo.LastMessagePostedOn,
            MemberTemplateModel.FromMember(convo.StartedBy),
            convo.State);
}

public record RoomTemplateModel(
    int Id,
    string? Name,
    string? PlatformRoomId,
    bool ManagedConversationsEnabled,
    IReadOnlyDictionary<string, string?> Metadata,
    RoomType RoomType = RoomType.Unknown)
{
    public static RoomTemplateModel FromRoom(Room room, IReadOnlyDictionary<string, string?> roomMetadata) =>
        new(room.Id,
            room.Name,
            room.PlatformRoomId,
            room.ManagedConversationsEnabled,
            roomMetadata,
            room.RoomType);
}

public record CustomerTemplateModel(int Id, string Name, IReadOnlyDictionary<string, string?> Metadata)
{
    public static CustomerTemplateModel? FromCustomer(Customer? customer, IReadOnlyDictionary<string, string?> customerMetadata)
        => customer is null
            ? null
            : new(customer.Id, customer.Name, customerMetadata);
}

public record MemberTemplateModel(
    int Id,
    bool Active,
    string DisplayName,
    string? Email,
    string? FormattedAddress,
    string? TimeZoneId,
    string PlatformUserId,
    string PlatformUrl,
    string? Avatar)
{
    public static MemberTemplateModel FromMember(Member member)
        => new(member.Id,
            member.Active,
            member.DisplayName,
            member.User.Email,
            member.FormattedAddress,
            member.TimeZoneId,
            member.User.PlatformUserId,
            member.FormatPlatformUrl().ToString(),
            member.User.Avatar);
}
