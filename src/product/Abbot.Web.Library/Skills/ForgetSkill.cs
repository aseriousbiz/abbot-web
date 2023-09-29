using System;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.Skills;

[Skill("forget", Description = "Forget an item remembered with the `rem` skill.")]
public sealed class ForgetSkill : ISkill
{
    readonly IMemoryRepository _memoryRepository;

    public ForgetSkill(IMemoryRepository memoryRepository)
    {
        _memoryRepository = memoryRepository;
    }

    public async Task OnMessageActivityAsync(
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        var (force, arguments) = messageContext
            .Arguments
            .FindAndRemove(a => a.Value.Equals("--force", StringComparison.OrdinalIgnoreCase));

        var (ignore, args) = arguments
            .FindAndRemove(a => a.Value.Equals("--ignore", StringComparison.OrdinalIgnoreCase));
        var key = args.Value;

        await (key.Length is 0
            ? messageContext.SendHelpTextAsync(this)
            : ignore is IMissingArgument
                ? ForgetItem(key, messageContext, force is not IMissingArgument)
                : IgnoreItem(key, messageContext));
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{phrase}", "forgets {phrase} added by the `rem` skill.");
        usage.Add("{phrase} --force", "forces the forget of {phrase} without prompting for confirmation.");
    }

    async Task ForgetItem(string name, MessageContext messageContext, bool force)
    {
        var retrieved = await _memoryRepository.GetAsync(name, messageContext.Organization);
        if (retrieved is null)
        {
            await messageContext.SendActivityAsync(
                $@"Nothing to forget. I don’t know anything about `{name}`.");
            return;
        }

        if (force)
        {
            await _memoryRepository.RemoveAsync(retrieved, messageContext.From);
            if (messageContext.IsInteraction && messageContext.InteractionInfo.Ephemeral)
            {
                if (messageContext.InteractionInfo.ResponseUrl is { } responseUrl)
                {
                    var activity = new RichActivity("Consider it forgotten!", new Section { Text = new MrkdwnText(":thumbsup: Consider it forgotten!") })
                    {
                        ResponseUrl = responseUrl
                    };
                    await messageContext.UpdateActivityAsync(activity);
                }
                else
                {
                    await messageContext.DeleteActivityAsync(messageContext.Room.PlatformRoomId, messageContext.InteractionInfo.ActivityId);
                }

                await messageContext.SendActivityAsync($"`{name}` forgotten by {messageContext.From.ToMention()}.");
            }
            else
            {
                await messageContext.SendActivityAsync($@"I forgot `{name}`.");
            }
        }
        else
        {
            var fallbackText = $@"Are you sure you want to forget `{name}`?";
            await messageContext.SendEphemeralActivityAsync(
                $@"Are you sure you want to forget `{name}`?",
                new Section { Text = new MrkdwnText(fallbackText) },
                new Actions
                {
                    BlockId = new BuiltInSkillCallbackInfo("forget"),
                    Elements = new[]{
                        new ButtonElement
                        {
                            Text = new PlainText("Yes"),
                            Value = $"{name} --force"
                        },
                        new ButtonElement
                        {
                            Text = new PlainText("Nevermind"),
                            Value = $"{name} --ignore"
                        }
                    }
                });
        }
    }

    static async Task IgnoreItem(string name, MessageContext messageContext)
    {
        if (messageContext.IsInteraction && messageContext.InteractionInfo.Ephemeral)
        {
            if (messageContext.InteractionInfo.ResponseUrl is { } responseUrl)
            {
                var activity = new RichActivity("Ok, I won't forget {name}.", new Section { Text = new MrkdwnText($":thumbsup: Ok, I won't forget {name}.") })
                {
                    ResponseUrl = responseUrl
                };
                await messageContext.UpdateActivityAsync(activity);
            }
            await messageContext.SendActivityAsync($@"{messageContext.From.ToMention()} decided to not forget `{name}`.");
        }
        else
        {
            await messageContext.SendActivityAsync($@"I won’t forget `{name}`.");
        }
    }
}
