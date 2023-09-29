using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Services;

/// <summary>
/// Dispatches messages directly to Slack rather than going through the Bot Framework rigamarole.
/// </summary>
public class SlackMessageDispatcher : IMessageDispatcher
{
    static readonly ILogger<SlackMessageDispatcher> Log = ApplicationLoggerFactory.CreateLogger<SlackMessageDispatcher>();

    readonly ISlackApiClient _slackApiClient;

    /// <summary>
    /// Constructs a <see cref="SlackMessageDispatcher"/> with an <see cref="ISlackApiClient"/> we can use to
    /// post directly to Slack.
    /// </summary>
    /// <param name="slackApiClient">The <see cref="ISlackApiClient"/> we'll use to post to Slack.</param>
    public SlackMessageDispatcher(ISlackApiClient slackApiClient)
    {
        _slackApiClient = slackApiClient;
    }

    public async Task<ProactiveBotMessageResponse> DispatchAsync(BotMessageRequest message, Organization organization)
    {
        var apiToken = organization.RequireAndRevealApiToken();

        var imageUpload = message.ImageUpload;

        if (imageUpload is not null)
        {
            // Special case: instead of posting a message, we'll post a file upload.
            try
            {
                var (channel, threadTimestamp) = (message.To.Id, message.To.ThreadId);

                var uploadResponse = await _slackApiClient.UploadAttachmentImageAsync(
                    apiToken,
                    imageUpload,
                    initialComment: message.Text,
                    channel,
                    threadTimestamp);

                return new ProactiveBotMessageResponse(
                    uploadResponse.Ok,
                    uploadResponse.Error ?? uploadResponse.Body?.Id ?? string.Empty);
            }
            catch (Exception ex)
            {
                // Log the error and continue with the message as best we can.
                Log.ErrorUploadingAttachment(ex, imageUpload.ImageBytes.Length, imageUpload.Title);
            }
        }

        var messageRequest = await CreateMessageRequestFromBotMessageRequestAsync(message);

        ApiResponse response;

        if (messageRequest.Timestamp is null)
        {
            if (messageRequest is not EphemeralMessageRequest ephemeralMessage)
            {
                response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
            }
            else
            {
                response = await _slackApiClient.PostEphemeralMessageWithRetryAsync(apiToken, ephemeralMessage);
            }
        }
        else
        {
            response = await _slackApiClient.UpdateMessageAsync(apiToken, messageRequest);
            if (response is { Ok: false, Error: "message_not_found" })
            {
                // Try again but as a new message.
                response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest with { Timestamp = null });
            }
        }

        return new ProactiveBotMessageResponse(response.Ok,
            response.Ok
                ? string.Empty
                : $"Failed to post message in Slack. Error: {response}",

            // If we updated a message, flow the timestamp back again.
            messageRequest.Timestamp ?? (response as MessageResponse)?.Body?.Timestamp);
    }

    /// <summary>
    /// Returns a <see cref="MessageRequest"/> populated from a <see cref="ProactiveBotMessage"/>. The
    /// <see cref="MessageRequest"/> is sent to the Slack chat.postMessage API to create a Slack message.
    /// </summary>
    /// <param name="message">The <see cref="ProactiveBotMessage"/> to send.</param>
    static async Task<MessageRequest> CreateMessageRequestFromBotMessageRequestAsync(BotMessageRequest message)
    {
        var text = SlackMessageFormatter.ConvertMarkdownLinksToSlackLinks(message.Text);
        var (channel, threadTimestamp) = (message.To.Id, message.To.ThreadId);

        var messageRequest = new MessageRequest(channel, text)
        {
            ThreadTs = threadTimestamp,
            Timestamp = message.To.MessageId,
            Blocks = message.Blocks?.ToList(),
            Attachments = message.Attachments?.ToList(),
            Metadata = message.MessageMetadata,
        };

        if (message is { To.EphemeralUser: { Length: > 0 } ephemeralUser })
        {
            messageRequest = new EphemeralMessageRequest(messageRequest)
            {
                User = ephemeralUser,
            };
        }
        return messageRequest;
    }
}

public static partial class SlackMessageDispatcherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Exception uploading base64 encoded attachment image of size {AttachmentLength} with title {Title} to Slack")]
    public static partial void ErrorUploadingAttachment(
        this ILogger<SlackMessageDispatcher> logger,
        Exception exception,
        int? attachmentLength,
        string? title);
}
