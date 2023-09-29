// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Serious.Payloads;
using Serious.Slack.Abstractions;
using Serious.Slack.BotFramework;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Microsoft.Bot.Builder.Adapters.Slack
{
    static class SlackHelper
    {
        const string SlackServiceUrl = "https://slack.botframework.com/";

        static readonly HashSet<string> MessageSubTypesThatAreEvents = new()
        {
            "channel_convert_to_private",
            "file_upload",
        };

        /// <summary>
        /// Formats a BotBuilder activity into an outgoing Slack message.
        /// </summary>
        /// <param name="activity">A BotBuilder Activity object.</param>
        /// <returns>A Slack message object with {text, attachments, channel, thread ts} as well as any fields found in activity.channelData.</returns>
        public static MessageChannelData ActivityToSlack(Activity activity)
        {
            // This should have been set by the Slack Formatter, but in the case of error messages, it might
            // not have been set.
            var channelData = activity.GetChannelData<MessageChannelData>();
            var message = channelData.Message;

            channelData = channelData with
            {
                Message = message with
                {
                    Channel = message.Channel is { Length: > 0 }
                        ? message.Channel
                        : activity.Conversation.Id,
                }
            };
            return channelData;
        }

        /// <summary>
        /// Creates an activity based on the slack payload. A payload is a JSON object that contains information
        /// about a user interaction with a Slack UI element.
        /// </summary>
        /// <param name="slackPayload">The payload of the slack event.</param>
        /// <returns>An activity containing the event data.</returns>
        public static Activity PayloadToActivity(IPayload slackPayload)
        {
            var originalMessage = slackPayload is IMessagePayload<SlackMessage> messagePayload
                ? messagePayload.Message
                : null;

            var activity = new Activity
            {
                Timestamp = default,
                ChannelId = "slack",
                Conversation = new ConversationAccount
                {
                    Id = SlackTranslator.GetConversationIdFromInteractionPayload(slackPayload),
                },
                From = new ChannelAccount(),
                ChannelData = slackPayload,
                Text = null,
                Type = slackPayload is IViewPayload or BlockSuggestionPayload
                    ? ActivityTypes.Event
                    : ActivityTypes.Message,
                Value = slackPayload,
                ServiceUrl = SlackServiceUrl
            };

            if (originalMessage?.ThreadTimestamp is not null)
            {
                activity.Conversation.Properties["thread_ts"] = originalMessage.ThreadTimestamp;
            }

            // Legacy behavior.
            if (slackPayload is InteractiveMessagePayload interactiveMessagePayload)
            {
                var actions = interactiveMessagePayload.PayloadActions;
                if (actions.Any())
                {
                    var action = actions[0];

                    activity.Text = action.Type switch
                    {
                        "button" => action.Value,
                        "select" => actions[0].SelectedOptions[0].Value ??
                                    actions[0].SelectedOption?.Value,
                        "static_select" => actions[0].SelectedOption?.Value,
                        _ => activity.Text
                    };

                    if (!string.IsNullOrEmpty(activity.Text))
                    {
                        activity.Type = ActivityTypes.Message;
                    }
                }
            }

            return activity;
        }

        /// <summary>
        /// Creates an activity based on the slack event data. Slack events provide information about activities that
        /// occur on Slack such as new messages, channel joins, and more.
        /// </summary>
        /// <param name="eventEnvelope">The data of the slack event.</param>
        /// <returns>An activity containing the event data.</returns>
        public static Activity EventToActivity(IEventEnvelope<EventBody> eventEnvelope)
        {
            var eventBody = eventEnvelope.Event;

            var channelId = eventEnvelope.Event switch
            {
                ReactionEvent r => r.Item.Channel ?? eventEnvelope.TeamId,
                MessageEvent m => m.Channel ?? m.ChannelId ?? eventEnvelope.TeamId,
                ChannelCreatedEvent c => c.Channel.Id,
                ChannelRenameEvent c => c.Channel.Id,
                ChannelLifecycleEvent cle => cle.Channel,
                _ => eventEnvelope.TeamId
            };

            var (type, action) = eventBody switch
            {
                AppUninstalledEvent => (ActivityTypes.InstallationUpdate, "remove"),
                MessageEvent msg when msg.User != "USLACKBOT" && (msg.SubType is null || !MessageSubTypesThatAreEvents.Contains(msg.SubType)) => (ActivityTypes.Message, null),
                _ => (ActivityTypes.Event, null)
            };

            var activity = new Activity
            {
                Type = type,
                Action = action,
                Id = eventBody.EventTimestamp,
                Timestamp = default,
                ChannelId = "slack",
                Conversation =
                    new ConversationAccount()
                    {
                        Id = channelId
                    },
                From = new ChannelAccount(),
                ChannelData = eventEnvelope,
                ServiceUrl = SlackServiceUrl,
                Recipient = new ChannelAccount(),
                Value = eventBody,
            };

            if (!string.IsNullOrEmpty(eventBody.ThreadTs))
            {
                activity.Conversation.Properties["thread_ts"] = eventBody.ThreadTs;
            }

            if (eventBody is MessageEvent message)
            {
                activity.Text = message.Text;
                if (((IPropertyBag)message).AdditionalProperties.TryGetValue("files", out var files) && files is JToken filesJson)
                {
                    var attachments = new List<Attachment>();
                    foreach (var attachment in filesJson)
                    {
                        var attachmentProperties = attachment.Value<JObject>()?.Properties();

                        var contentType = string.Empty;
                        var contentUrl = string.Empty;
                        var name = string.Empty;

                        if (attachmentProperties != null)
                        {
                            foreach (var property in attachmentProperties)
                            {
                                switch (property.Name)
                                {
                                    case "mimetype":
                                        contentType = property.Value.ToString();
                                        break;
                                    case "url_private_download":
                                        contentUrl = property.Value.ToString();
                                        break;
                                    case "name":
                                        name = property.Value.ToString();
                                        break;
                                }
                            }
                        }
                        attachments.Add(new Attachment
                        {
                            ContentType = contentType,
                            ContentUrl = contentUrl,
                            Name = name
                        });
                    }

                    activity.Attachments = attachments;
                }
            }
            else
            {
                activity.Name = eventBody.Type;
            }

            return activity;
        }

        /// <summary>
        /// Creates an activity based on a slack event related to a slash command.
        /// </summary>
        /// <param name="commandRequest">The data of the slack command request.</param>
        /// <returns>An activity containing the event data.</returns>
        public static Activity CommandToActivity(CommandPayload commandRequest)
        {
            var activity = new Activity
            {
                Id = commandRequest.TriggerId,
                Timestamp = default,
                ChannelId = "slack",
                Conversation = new ConversationAccount()
                {
                    Id = commandRequest.ChannelId,
                },
                From = new ChannelAccount(),
                ChannelData = commandRequest,
                Type = ActivityTypes.Event,
                Name = "Command",
                Value = commandRequest.Command,
                Recipient = new ChannelAccount(),
            };

            activity.Conversation.Properties["team"] = commandRequest.TeamId;

            return activity;
        }

        /// <summary>
        /// Converts a query string to a dictionary with key-value pairs.
        /// </summary>
        /// <param name="query">The query string to convert.</param>
        /// <returns>A dictionary with the query values.</returns>
        public static Dictionary<string, string> QueryStringToDictionary(string query)
        {
            var values = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return values;
            }

            var pairs = query.Replace("+", "%20", StringComparison.Ordinal).Split('&');
            foreach (var p in pairs)
            {
                var pair = p.Split('=');
                var key = pair[0];
                var value = Uri.UnescapeDataString(pair[1]);

                values.Add(key, value);
            }

            return values;
        }
    }
}
