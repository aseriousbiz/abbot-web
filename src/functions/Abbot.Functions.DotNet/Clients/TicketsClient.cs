using System;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Clients;

public class TicketsClient : ITicketsClient
{
    readonly ISkillApiClient _apiClient;
    readonly ISkillContextAccessor _skillContextAccessor;

    public TicketsClient(ISkillApiClient apiClient, ISkillContextAccessor skillContextAccessor)
    {
        _apiClient = apiClient;
        _skillContextAccessor = skillContextAccessor;
    }

    public async Task<IResult> ReplyWithTicketPromptAsync()
    {
        var uri = GetDataUri("/buttons");

        var skillContext = _skillContextAccessor.SkillContext.Require();
        int? conversationId = int.TryParse(skillContext.ConversationInfo?.Id, out var id)
            ? id
            : null;

        var recipient = skillContext.SkillInfo.From.Id;
        var message = skillContext.SkillInfo.Message;
        var messageId = message?.MessageId
#pragma warning disable CS0618
                        ?? skillContext.SkillInfo.MessageId;
#pragma warning restore CS0618

        var conversationIdentifier = new ConversationIdentifier(
            Channel: skillContext.SkillInfo.Room.Id,
            MessageId: message?.ThreadId
#pragma warning disable CS0618
                       ?? skillContext.SkillInfo.ThreadId
#pragma warning restore CS0618
                       ?? messageId,
            conversationId);

        return await _apiClient.PostJsonAsync<TicketPromptRequest, ApiResult>(
                uri,
                data: new TicketPromptRequest(
                    User: recipient,
                    messageId,
                    conversationIdentifier))
            .Require();
    }

    Uri GetDataUri(string action) => _apiClient.BaseApiUrl.Append("/ticket").Append(action);
}
