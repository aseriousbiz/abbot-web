using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models.Api;

public record ConversationResponseModel(
    int Id,
    RenderedMessageResponseModel Title,
    DateTime LastMessagePostedOn,
    Uri? FirstMessageUrl,
    MemberResponseModel StartedBy,
    RoomResponseModel Room,
    IReadOnlyList<ConversationMemberResponseModel> Members)
{
    public static ConversationResponseModel Create(ConversationViewModel convoModel, Member? viewer = null) =>
        new(
            convoModel.Conversation.Id,
            RenderedMessageResponseModel.Create(convoModel.Conversation.Title, convoModel.Title, viewer),
            convoModel.Conversation.LastMessagePostedOn,
            convoModel.Conversation.GetFirstMessageUrl(),
            MemberResponseModel.Create(convoModel.Conversation.StartedBy, viewer),
            RoomResponseModel.Create(convoModel.Conversation.Room, viewer),
            convoModel.Conversation.Members.Select(m => ConversationMemberResponseModel.Create(m, viewer)).ToList());
}
