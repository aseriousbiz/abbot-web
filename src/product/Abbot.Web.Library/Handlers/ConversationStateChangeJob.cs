using System;
using System.Threading.Tasks;
using Hangfire;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Used to revert a conversation after a snooze period. This method is called by the background task.
/// </summary>
public class ConversationStateChangeJob
{
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly IClock _clock;

    public ConversationStateChangeJob(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        ISlackApiClient slackApiClient,
        IClock clock)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _slackApiClient = slackApiClient;
        _clock = clock;
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    [Obsolete("Use Id<> overload")]
    public Task WakeAsync(int conversationId, int memberId) =>
        WakeAsync(new(conversationId), new(memberId));

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task WakeAsync(Id<Conversation> conversationId, Id<Member> memberId)
    {
        if (await _conversationRepository.GetConversationAsync(conversationId) is { State: ConversationState.Snoozed } conversation
            && await _userRepository.GetMemberByIdAsync(memberId, conversation.Organization) is { } from)
        {
            var abbotMember = await _userRepository.EnsureAbbotMemberAsync(conversation.Organization);

            // Move it back to NeedsResponse.
            await _conversationRepository.WakeConversationAsync(
                conversation,
                abbotMember,
                _clock.UtcNow);

            var messageUrl = conversation.GetFirstMessageUrl();
            var hyperLink = new Hyperlink(messageUrl, "snooze a conversation");

            // And DM the user that snoozed it.
            var response = await _slackApiClient.SendDirectMessageAsync(
                from.Organization,
                from.User,
                "Hi, you asked me to snooze a conversation for an hour. I moved it back to the Needs Response state.",
                new Section(new MrkdwnText($"Hi, you asked me to {hyperLink} for an hour. I moved it back to the Needs Response state.")));

            if (!response.Ok)
            {
                throw new InvalidOperationException(response.ToString());
            }
        }
    }
}
