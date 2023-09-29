using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models.Api;

public record ConversationListResponseModel(
    IReadOnlyList<ConversationResponseModel> Conversations,
    ConversationStats Stats,
    PaginationResponseModel Pagination)
{
    public static ConversationListResponseModel Create(IEnumerable<ConversationViewModel> models, ConversationListWithStats list, Member? viewer = null) =>
        new(
            models.Select(m => ConversationResponseModel.Create(m, viewer)).ToList(),
            list.Stats,
            PaginationResponseModel.Create(list.Conversations));
}
