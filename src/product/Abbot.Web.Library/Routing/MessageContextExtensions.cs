using Serious.Abbot.Messaging;
using Serious.Abbot.Metadata;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.Infrastructure;

public static class MessageContextExtensions
{
    /// <summary>
    /// Sends a rich formatted message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="messageContext">An <see cref="MessageContext"/> representing the current incoming message and environment.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task SendActivityAsync(
        this MessageContext messageContext,
        string fallbackText,
        params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks);
        await messageContext.SendActivityAsync(richActivity);
    }

    /// <summary>
    /// Sends a rich formatted ephemeral message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="messageContext">An <see cref="MessageContext"/> representing the current incoming message and environment.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task SendEphemeralActivityAsync(
        this MessageContext messageContext,
        string fallbackText,
        params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks)
        {
            EphemeralUser = messageContext.From.PlatformUserId
        };
        await messageContext.SendActivityAsync(richActivity);
    }

    /// <summary>
    /// Updates a rich formatted message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="messageContext">An <see cref="MessageContext"/> representing the current incoming message and environment.</param>
    /// <param name="activityId">The Id of the message to update.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task UpdateActivityAsync(
        this MessageContext messageContext,
        string activityId,
        string fallbackText,
        params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks)
        {
            Id = activityId
        };
        await messageContext.UpdateActivityAsync(richActivity);
    }

    /// <summary>
    /// Updates a rich formatted message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="messageContext">An <see cref="MessageContext"/> representing the current incoming message and environment.</param>
    /// <param name="responseUrl">For some payloads, such as ephemeral messages, this URL provides an endpoint to edit or delete the message.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task UpdateActivityAsync(
        this MessageContext messageContext,
        Uri responseUrl,
        string fallbackText,
        params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks)
        {
            ResponseUrl = responseUrl
        };
        await messageContext.UpdateActivityAsync(richActivity);
    }

    /// <summary>
    /// Formats the current room as a room mention.
    /// </summary>
    /// <param name="messageContext">The current message context.</param>
    public static string FormatRoomMention(this MessageContext messageContext)
        => messageContext.Room.ToMention();

    public static Task SendHelpTextAsync(this MessageContext messageContext,
        ISkill skill)
    {
        return messageContext.SendHelpTextAsync(skill, messageContext.SkillName);
    }

    public static async Task SendHelpTextAsync(this MessageContext messageContext,
        IResolvedSkill resolvedSkill,
        string skillNameOverride)
    {
        bool useSkillContainerHelp = resolvedSkill.Name.Equals(skillNameOverride, StringComparison.Ordinal);

        if (resolvedSkill.Skill is RemoteSkillCallSkill remoteSkillCallSkill)
        {
            var usage = await remoteSkillCallSkill.GetSkillUsageText(
                resolvedSkill.Name,
                messageContext.Organization);
            await messageContext.SendActivityAsync(usage);
            return;
        }

        var usageText = GetHelpText(
            resolvedSkill.Skill,
            skillNameOverride,
            useSkillContainerHelp,
            messageContext.Bot);
        var helpText = resolvedSkill.Description.DefaultIfEmpty("(_no description_)")
            .AppendIfNotEmpty(usageText, "\nUsage:\n");
        await messageContext.SendActivityAsync(helpText);
    }

    static Task SendHelpTextAsync(this MessageContext messageContext,
        ISkill skill,
        string skillNameOverride)
    {
        var helpText = GetHelpText(
            skill,
            skillNameOverride,
            skillNameOverride.Equals(messageContext.SkillName, StringComparison.Ordinal),
            messageContext.Bot);
        return messageContext.SendActivityAsync(helpText);
    }

    static string GetHelpText(
        ISkill skill,
        string skillName,
        bool useSkillContainerHelp,
        BotChannelUser bot)
    {
        var usageBuilder = UsageBuilder.Create(skillName, bot);
        if (!useSkillContainerHelp && skill is ISkillContainer skillContainer)
        {
            skillContainer.BuildSkillUsageHelp(usageBuilder);
        }
        else
        {
            skill.BuildUsageHelp(usageBuilder);
        }
        return usageBuilder.ToString();
    }
}
