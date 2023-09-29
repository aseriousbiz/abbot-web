using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.TestHelpers;

namespace Abbot.Common.TestHelpers;

public record ConversationFixture(string Title, DateTime LastStateChangedDate, ConversationState State = ConversationState.New);

public record ConversationEnvironment(
    TestEnvironmentWithData Environment,
    Member FirstResponder,
    Member Customer,
    Conversation Conversation,
    DateTime NowUtc)
{
    public Room Room => Conversation.Room;

    public async Task RunScriptAsync(IEnumerable<ConversationStep> actions)
    {
        foreach (var action in actions)
        {
            var date = NowUtc.AddDays(-1 * action.DaysAgo);

            switch (action.ConversationAction)
            {
                case ConversationAction.CustomerResponds
                    or ConversationAction.FirstResponderResponds:
                    {
                        if (action.ConversationAction is ConversationAction.CustomerResponds)
                        {
                            await CustomerResponds(date);
                        }
                        else
                        {
                            await FirstResponderResponds(date);
                        }
                        break;
                    }
                case ConversationAction.ConversationOverdue:
                    {
                        await Overdue(date);

                        break;
                    }
                case ConversationAction.ConversationClosed:
                    await Close(date);

                    break;

                case ConversationAction.ConversationSnoozed:
                    await Snooze(date);
                    break;

                case ConversationAction.ConversationAwakened:
                    await Awaken(date);
                    break;
            }
        }
    }

    DateTime ToUtc(string shortDateUtc) => Dates.ParseUtc($"{NowUtc.Year}, " + shortDateUtc);

    public Task Close(string shortDateUtc) => Close(ToUtc(shortDateUtc));

    public Task Snooze(string shortDateUtc) => Snooze(ToUtc(shortDateUtc));

    public Task Snooze(DateTime snoozeDateUtc) =>
        Environment.Conversations.SnoozeConversationAsync(Conversation, FirstResponder, snoozeDateUtc);

    public Task Awaken(string shortDateUtc) => Awaken(ToUtc(shortDateUtc));

    public Task Awaken(DateTime wakeDate) =>
        Environment.Conversations.WakeConversationAsync(Conversation, FirstResponder, wakeDate);

    public async Task Close(DateTime closeDateUtc)
    {
        await Environment.Conversations.CloseAsync(
            Conversation,
            FirstResponder,
            closeDateUtc,
            "unit test");
    }

    public Task Overdue(string shortDateUtc) => Overdue(ToUtc(shortDateUtc));

    public async Task Overdue(DateTime overdueDateUtc)
    {
        var abbotMember = Environment.TestData.Abbot;
        await Environment.Conversations.UpdateOverdueConversationAsync(Conversation,
            overdueDateUtc,
            abbotMember);
    }

    public Task CustomerResponds(string shortDateUtc) => CustomerResponds(ToUtc(shortDateUtc));

    public async Task CustomerResponds(DateTime respondDateUtc)
    {
        await MemberResponds(respondDateUtc, Customer, "Customer response!", true);
    }

    public Task FirstResponderResponds(string shortDateUtc) => FirstResponderResponds(ToUtc(shortDateUtc));

    public async Task FirstResponderResponds(DateTime respondDateUtc)
    {
        await MemberResponds(respondDateUtc, FirstResponder, "Responder response!", false);
    }

    async Task MemberResponds(
        DateTime respondDateUtc,
        Member from,
        string text,
        bool posterIsSupportee)
    {
        var messageId = Environment.IdGenerator.GetSlackMessageId();
        await Environment.Conversations.UpdateForNewMessageAsync(
            Conversation,
            new MessagePostedEvent
            {
                MessageId = messageId,
            },
            Environment.CreateConversationMessage(Conversation, respondDateUtc, text: text, messageId: messageId),
            posterIsSupportee);
    }
}
