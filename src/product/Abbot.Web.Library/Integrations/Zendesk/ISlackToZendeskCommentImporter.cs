using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations.Zendesk;

/// <summary>
/// Used to import a set of messages into Zendesk from Slack.
/// </summary>
public interface ISlackToZendeskCommentImporter
{
    /// <summary>
    /// Imports a set of messages into Zendesk.
    /// </summary>
    /// <param name="conversation">The conversation where the messages occurred.</param>
    /// <param name="messages">The set of messages to import.</param>
    Task ImportThreadAsync(Conversation conversation, IEnumerable<ConversationMessage> messages);
}
