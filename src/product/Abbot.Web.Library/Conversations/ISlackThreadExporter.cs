using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Conversations;

/// <summary>
/// A service that can export a Slack thread into a stored Setting temporarily, and then
/// later used to retrieve the messages in the thread from the setting.
/// </summary>
/// <remarks>
/// This class is used for Zendesk, but it's generic enough to be used anywhere we need
/// to store a thread of messages temporarily.
/// </remarks>
public interface ISlackThreadExporter
{
    /// <summary>
    /// Exports the Slack thread to a setting value so we can import it later.
    /// </summary>
    /// <param name="ts">The timestamp of the root message.</param>
    /// <param name="channel">The channel where the message is in.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="actor">The user exporting the thread.</param>
    Task<Setting?> ExportThreadAsync(string ts, string channel, Organization organization, Member actor);

    /// <summary>
    /// Retrieves the exported messages from the setting where it was stored and deletes the setting.
    /// </summary>
    /// <param name="settingName">The name of the setting where the messages are stored.</param>
    /// <param name="organization">The organization.</param>
    /// <returns>The set of messages stored.</returns>
    Task<IReadOnlyList<SlackMessage>> RetrieveMessagesAsync(string settingName, Organization organization);
}
