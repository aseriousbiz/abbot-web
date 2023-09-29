using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// Normalize incoming and format outgoing messages for Slack.
/// </summary>
public class SlackMessageFormatter : IMessageFormatter
{
    /// <summary>
    /// Given outgoing message text in Abbot format, formats the text for Slack by populating Slack's channel data.
    /// </summary>
    /// <param name="activity">The outgoing activity.</param>
    /// <param name="turnContext">The context object for this turn.</param>
    public void FormatOutgoingMessage(Activity activity, ITurnContext turnContext)
    {
        var format = activity.TextFormat ?? string.Empty;

        if (activity.Type is not "message" || format is { Length: > 0 } and not "markdown")
        {
            return;
        }

        if (!turnContext.TryGetApiToken(out var apiToken))
        {
            throw new InvalidOperationException("No Slack API Token provided");
        }

        if (activity.ChannelData is not MessageChannelData)
        {
            var (blocks, ephemeralUser, responseUrl) = activity is RichActivity richActivity
                ? (richActivity.Blocks, richActivity.EphemeralUser, richActivity.ResponseUrl)
                : (null, null, null);

            // If responseUrl is not null, then this is not a DM and we don't need address info such as
            // channel and thread timestamp.
            var (threadTimestamp, channel) = responseUrl is null
                ? (SlackConversationId.TryParse(activity.Conversation.Id, out var convoId)
                       && convoId.ThreadTimestamp is { Length: > 0 } threadTs
                        ? threadTs
                        : null,
                    convoId.ChannelId)
                : (null, string.Empty);

            var attachment = activity.Attachments?.SingleOrDefault();

            var attachments = attachment is { ContentType: "image/gif" } gif
                ? new[] {
                    new LegacyMessageAttachment
                    {
                        // Special case so our .ping skill responds with a Pong gif.
                        Title = "Pong",
                        ImageUrl = gif.ContentUrl is { }
                            ? new Uri(gif.ContentUrl)
                            : null,
                        Color = "#000066"
                    }
                }
                : null;

            var messageRequest = new MessageRequest
            {
                Timestamp = activity.Id,
                Channel = channel,
                Text = ConvertMarkdownLinksToSlackLinks(activity.Text ?? string.Empty),
                ThreadTs = threadTimestamp,
                Blocks = blocks,
                Attachments = attachments,
            };

            activity.ChannelData = new MessageChannelData(apiToken, messageRequest, responseUrl)
            {
                EphemeralUser = ephemeralUser
            };
            activity.Text = null;


        }
    }

    /// <summary>
    /// Grabs the message from Slack Channel Data. We're using the native Slack message format as our
    /// normalized Abbot format.
    /// </summary>
    /// <param name="activity">The incoming message.</param>
    public void NormalizeIncomingMessage(Activity activity)
    {
        if (activity.ChannelData is null or BlockActionsPayload)
        {
            return;
        }

        var slackChannelData = JObject
            .FromObject(activity.ChannelData)
            .ToObject<SlackChannelData>();

        var slackMessage = slackChannelData?.SlackMessage;

        if (slackMessage is EventEnvelope<MessageEvent> { Event.Text.Length: > 0 } messageEvent)
        {
            activity.Text = NormalizeIncomingEmails(messageEvent.Event.Text);
        }
    }

    static readonly Regex EmailRegex = new(@"<(?:mailto:)?(?<email>[^@]+@.+?)\|.+?>",
        RegexOptions.Compiled);

    static string NormalizeIncomingEmails(string text)
    {
        return EmailRegex.Replace(text, match => match.Groups["email"].Value);
    }

    static readonly Regex LinkRegex = new(@"\[(?<text>[^\r^\n]+?)\]\((?<url>https?://[\w:\d./?=#]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Converts any markdown links in the text into Slack links.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>A string with the converted text.</returns>
    public static string ConvertMarkdownLinksToSlackLinks(string? text)
    {
        return LinkRegex.Replace(text ?? string.Empty, "<${url}|${text}>");
    }
}
