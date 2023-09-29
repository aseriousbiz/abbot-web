using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Conversations;

public class SlackThreadExporter : ISlackThreadExporter
{
    static readonly ILogger<SlackThreadExporter> Log = ApplicationLoggerFactory.CreateLogger<SlackThreadExporter>();

    readonly ISlackApiClient _slackApiClient;
    readonly ISettingsManager _settingsManager;

    public SlackThreadExporter(ISlackApiClient slackApiClient, ISettingsManager settingsManager)
    {
        _slackApiClient = slackApiClient;
        _settingsManager = settingsManager;
    }

    /// <summary>
    /// Exports the Slack thread to a setting value so we can import it later.
    /// </summary>
    /// <param name="ts">The timestamp of the root message.</param>
    /// <param name="channel">The channel where the message is in.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="actor">The user exporting the thread.</param>
    public async Task<Setting?> ExportThreadAsync(string ts, string channel, Organization organization, Member actor)
    {
        var apiToken = organization.RequireAndRevealApiToken();

        // Get the most recent replies for this message.
        var response = await _slackApiClient.Conversations.GetConversationRepliesAsync(
            apiToken,
            channel,
            ts,
            limit: 1000);

        if (!response.Ok)
        {
            Log.RetrievingReplies(
                response.ToString(),
                ts,
                channel);
            return null;
        }

        if (response.Body is not { } messages)
        {
            Log.RetrievingReplies(
                "Response body was null retrieving messages.",
                ts,
                channel);
            return null;
        }

        if (messages is { Count: 0 })
        {
            // No replies to import.
            return null;
        }

        var messagesJson = JsonConvert.SerializeObject(messages);

        return await _settingsManager.SetAsync(
            SettingsScope.Organization(organization),
            $"Slack.Thread.Export:{ts}:{channel}",
            messagesJson,
            actor.User);
    }

    public async Task<IReadOnlyList<SlackMessage>> RetrieveMessagesAsync(string settingName, Organization organization)
    {
        var exportedMessagesSetting = await _settingsManager.GetAsync(
            SettingsScope.Organization(organization),
            settingName);

        if (exportedMessagesSetting is { } setting)
        {
            await _settingsManager.RemoveAsync(SettingsScope.Organization(organization), setting.Name, setting.Creator);
        }

        return exportedMessagesSetting is not { Value: { } messagesJson }
            ? Array.Empty<SlackMessage>()
            : JsonConvert.DeserializeObject<IReadOnlyList<SlackMessage>>(messagesJson) ?? Array.Empty<SlackMessage>();
    }

}

static partial class SlackThreadExporterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "{Error} occured trying to retrieve replies for message {Timestamp} in channel {Channel}.")]
    public static partial void RetrievingReplies(
        this ILogger<SlackThreadExporter> logger,
        string error,
        string timestamp,
        string channel);
}
