using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messages;
using Serious.Abbot.PayloadHandlers;
using Serious.Cryptography;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.Text;

namespace Serious.Abbot.Messaging;

public static class ProactiveBotMessageExtensions
{
    public static BotMessageRequest TranslateToRequest(this ProactiveBotMessage message)
    {
        var imageUpload = message.Attachments is [var attachment]
                          && attachment.ImageUrl?.TryGetBytesFromBase64EncodedFile(out var imageBytes) is true
            ? new ImageUpload(imageBytes, attachment.Title)
            : null;

        var chatAddress = message.Options is { To: { } to }
            ? to
            : message.ConversationReference.TranslateConversationReference().Require();

        var metadata = new MessageMetadata
        {
            EventType = "skill_message",
            EventPayload = new Dictionary<string, object?>
            {
                ["skill_id"] = message.SkillId,
            },
        };

        var messageRequest = new BotMessageRequest(
            message.Message,
            chatAddress,
            message.DeserializeBlocks(),
            message.Attachments is null ? null : message.TranslateAttachments(),
            imageUpload,
            metadata);

        return messageRequest;
    }

    static ChatAddress? TranslateConversationReference(this ConversationReference conversationReference)
    {
        if (!SlackConversationId.TryParse(conversationReference.Conversation.Id, out var slackConversationId))
        {
            throw new InvalidOperationException($"Conversation reference Id {conversationReference.Conversation.Id} isn't a valid SlackConversationId");
        }

        var (channel, threadTimestamp) = slackConversationId;

        return new ChatAddress(ChatAddressType.Room, channel, threadTimestamp);
    }

    /// <summary>
    /// Deserializes the Blocks property into a list of layout blocks, preserving some important behaviors needed
    /// to make this work for skills.
    /// </summary>
    /// <returns>A list of <see cref="ILayoutBlock"/> instances or <c>null</c> if <see cref="AnnouncementHandler.Blocks"/> is <c>null</c>.</returns>
    static IEnumerable<ILayoutBlock>? DeserializeBlocks(this ProactiveBotMessage message)
    {
        var blocks = DeserializeBlocks(message.Blocks);
        return RewriteBlockIds(blocks, new(message.SkillId), message.ContextId)?.ToList();
    }

    static IEnumerable<ILayoutBlock>? DeserializeBlocks(string? blocksJson)
    {
        if (string.IsNullOrEmpty(blocksJson))
        {
            return null;
        }

        var layoutJObject = JsonConvert.DeserializeObject<JToken>(blocksJson);
        if (layoutJObject is null)
        {
            return null;
        }

        // If they created an object with top level blocks, use the blocks part.
        layoutJObject = layoutJObject is JObject jObject
                        && jObject.ContainsKey("blocks")
                        && jObject["blocks"] is JArray jArray
            ? jArray
            : layoutJObject;

        if (layoutJObject is JArray array)
        {
            var blocks = array.ToObject<ILayoutBlock[]>();
            return blocks is { Length: > 0 }
                ? blocks
                : null;
        }
        var layoutObject = layoutJObject.ToObject<ILayoutBlock>();
        return layoutObject is null
            ? null
            : new[] { layoutObject };
    }

    static IEnumerable<ILayoutBlock>? RewriteBlockIds(IEnumerable<ILayoutBlock>? blocks, Id<Skill> skillId, string? contextId)
    {
        return blocks?.OfType<LayoutBlock>().Select(block => block with
        {
            BlockId = WrapBlockId(block.BlockId, skillId, contextId)
        });
    }

    // Since we're setting the block id with our routing information, we need to make sure every
    // block Id is unique. If the user didn't supply one, we should generate one.
    // If we didn't set a block id, Slack would set a random one anyways.
    static string WrapBlockId(string? blockId, Id<Skill> skillId, string? contextId)
    {
        var originalValue = blockId ?? TokenCreator.CreateRandomString(4);
        if (CallbackInfo.TryParse(originalValue, out _))
        {
            // This is our own special routing. We don't want to hide it.
            // What this means is a customer could have a block id that starts with "i:" and we wouldn't route it
            // properly. We'll worry about that later.
            // It also opens up the possibility for customer created blocks that route to our built-in handlers.
            // That could actually be cool.
            return originalValue;
        }
        var callbackInfo = new UserSkillCallbackInfo(skillId, contextId);
        return new WrappedValue(callbackInfo, originalValue);
    }

    static IEnumerable<LegacyMessageAttachment> TranslateAttachments(this ProactiveBotMessage message)
    {
        if (message.Attachments is [var attachment, ..]) // Ignore all but first attachment
        {
            var title = attachment.Title;
            var color = attachment.Color;
            var titleLink = attachment.TitleUrl;

            if (attachment.ImageUrl is { Length: > 0 })
            {
                yield return new LegacyMessageAttachment
                {
                    Title = title,
                    TitleLink = titleLink,
                    // TODO: If the ImageUrl is not a valid Uri, it would be nice if we could replace it with an
                    // image that says exactly that "ImageUrl is not valid". But for now, we silently ignore it.
                    ImageUrl = attachment.ImageUrl is { }
                               && Uri.TryCreate(attachment.ImageUrl, UriKind.Absolute, out var imageUri)
                        ? imageUri
                        : null,
                    Fallback = attachment.Title ?? title ?? "Unspecified",
                    Color = color
                };
                title = null; // We'll use it for the image.
            }

            if (attachment.Buttons is { Count: > 0 })
            {
                yield return ToSlackAttachment(attachment.Buttons, title, color, new(message.SkillId), message.ContextId ?? "");
            }
        }
    }

    static LegacyMessageAttachment ToSlackAttachment(
        IEnumerable<ButtonMessage> buttons,
        string? title,
        string? color,
        Id<Skill> skillId,
        string contextId)
    {
        var callbackInfo = new UserSkillCallbackInfo(skillId, contextId);
        return new LegacyMessageAttachment
        {
            CallbackId = callbackInfo.ToString(),
            Title = title, // If it wasn't used for the first image, we'll use it here.
            Actions = buttons.Select(ToSlackAction).ToList(),
            Color = color
        };
    }

    static AttachmentAction ToSlackAction(ButtonMessage button)
    {
        return new()
        {
            Name = "choices",
            Text = button.Title,
            Value = button.Arguments,
            Style = button.Style
        };
    }
}
