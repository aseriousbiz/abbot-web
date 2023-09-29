using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models.Api;

public record ConversationMemberResponseModel(
    MemberResponseModel Member,
    DateTime JoinedConversationAt,
    DateTime LastPostedAt)
{
    public static ConversationMemberResponseModel Create(ConversationMember member, Member? viewer = null) =>
        new(
            MemberResponseModel.Create(member.Member, viewer),
            member.JoinedConversationAt,
            member.LastPostedAt);
}
