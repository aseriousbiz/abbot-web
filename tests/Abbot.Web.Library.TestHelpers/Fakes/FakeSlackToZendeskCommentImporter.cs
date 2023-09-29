using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeSlackToZendeskCommentImporter : ISlackToZendeskCommentImporter
{
    public Conversation? ImportedConversation { get; private set; }

    public IReadOnlyList<ConversationMessage> ImportedMessages { get; private set; } = new List<ConversationMessage>();

    public Task ImportThreadAsync(Conversation conversation, IEnumerable<ConversationMessage> messages)
    {
        ImportedConversation = conversation;
        ImportedMessages = messages.ToList();
        return Task.CompletedTask;
    }
}
