using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Skills;

/// <summary>
/// Built-in skill that is used to call user defined skills.
/// </summary>
[Skill(SkillName, Description = "Call a user skill created in the web skill editor.", Hidden = true)]
public sealed class RemoteSkillCallSkill : ISkill
{
    static readonly ILogger<RemoteSkillCallSkill> Log = ApplicationLoggerFactory.CreateLogger<RemoteSkillCallSkill>();

    public const string SkillName = "remoteskillcall";
    readonly ISkillRunnerClient _skillRunnerClient;
    readonly ISkillRepository _skillRepository;
    readonly ISlackResolver _slackResolver;
    readonly IUrlGenerator _urlGenerator;
    readonly ArgumentRecognizer _argumentRecognizer;
    readonly ISensitiveLogDataProtector _dataProtector;

    public RemoteSkillCallSkill(
        ISkillRunnerClient skillRunnerClient,
        ISkillRepository skillRepository,
        ISlackResolver slackResolver,
        IUrlGenerator urlGenerator,
        ArgumentRecognizer argumentRecognizer,
        ISensitiveLogDataProtector dataProtector)
    {
        _skillRunnerClient = skillRunnerClient;
        _skillRepository = skillRepository;
        _slackResolver = slackResolver;
        _urlGenerator = urlGenerator;
        _argumentRecognizer = argumentRecognizer;
        _dataProtector = dataProtector;
    }

    /// <summary>
    /// Handles the incoming message.
    /// </summary>
    /// <param name="messageContext">Information about the message.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        if (messageContext.Patterns.Count > 0)
        {
            // If any patterns match, the skill is already loaded and we can be sure it's active and enabled.
            // We don't do argument extraction here (yet), because Patterns are their own thing (for now 😜).
            foreach (var pattern in messageContext.Patterns)
            {
                using var __ = Log.BeginSkillScope(pattern.Skill);
                await CallSkillAsync(
                    messageContext,
                    pattern.Skill,
                    messageContext.Arguments,
                    messageContext.OriginalMessage,
                    new()
                    {
                        CommandText = messageContext.OriginalMessage,
                    },
                    pattern);
            }

            return;
        }

        var (skillName, arguments) = messageContext.Arguments.Pop();
        var skill = await _skillRepository.GetAsync(skillName, messageContext.Organization);
        if (skill is null)
        {
            await messageContext.SendActivityAsync(
                $"There is no skill named `{skillName}`.");

            return;
        }

        using var _ = Log.BeginSkillScope(skill);

        // Perform AI argument recognition.
        // But first, check the sigil at the end of the skill name.
        // A "!" indicates that arguments should be treated verbatim.
        ArgumentRecognitionResult? recognitionResult = null;
        if (messageContext.Sigil != "!" && skill.Properties.ArgumentExtractionEnabled == true)
        {
            try
            {
                // Perform argument recognition!
                recognitionResult = await _argumentRecognizer.RecognizeArgumentsAsync(
                    skill,
                    skill.Exemplars,
                    arguments.ToString() ?? string.Empty,
                    messageContext.FromMember);

                // Reconstitute new arguments using the result and the existing mentioned users.
                // If the AI extracts a user mentioned in the original message as an argument, we want to preserve that mention.
                // If the AI synthesizes a new user mention, we won't preserve it, it'll just be text, but that's probably OK.
                arguments = new Arguments(
                    recognitionResult.Arguments,
                    messageContext.Mentions.Select(m => m.ToPlatformUser()));

                // Post the recognized arguments to the original Slack thread
                // We don't want Mentions to actually render and annoy the mentioned user
                // so we'll format them as @username.
                var formatForDisplay = string.Join(" ",
                    arguments.Select(a =>
                        a switch {
                            IMentionArgument mention => $"@{mention.Mentioned.Name}",
                            IRoomArgument room => $"#{room.Room.Name}",
                            _ => a.ToString()
                        }));

                await messageContext.SendActivityAsync(
                    $"""
                    :robot_face: From your message, I inferred the following arguments
                    ```
                    {formatForDisplay}
                    ```
                    """,
                    inThread: true);
            }
            catch (Exception ex)
            {
                await messageContext.SendActivityAsync(
                    $"Argument recognition for `{skillName}` failed. Use `{skillName}!` to skip recognition and pass arguments directly. {WebConstants.GetContactSupportSentence()}");

                Log.FailedToRecognizeArguments(ex, _dataProtector.Protect(arguments.ToString()));
                return;
            }
        }

        await CallSkillAsync(
            messageContext,
            skill,
            arguments,
            messageContext.CommandText,
            new()
            {
                CommandText = messageContext.OriginalMessage,
                ArgumentRecognitionResult = recognitionResult,
            });
    }

    async Task CallSkillAsync(
        MessageContext messageContext,
        Skill skill,
        IArguments arguments,
        string commandText,
        SkillRunProperties auditProperties,
        IPattern? pattern = null)
    {
        try
        {
            var response = await RunRemoteSkillAsync(
                messageContext,
                skill.Name,
                skill,
                arguments,
                commandText,
                pattern,
                auditProperties);

            var replies = response.Replies ?? Array.Empty<string>();

            if (response.Success)
            {
                foreach (var reply in replies)
                {
                    await messageContext.SendActivityAsync(
                        string.IsNullOrWhiteSpace(reply)
                            ? "An empty reply was returned by the Bot skill."
                            : reply);
                }
            }
            else
            {
                if (response.Replies is { Count: > 0 })
                {
                    await messageContext.SendActivityAsync(string.Join("\n", response.Replies));
                }
                else
                {
                    await messageContext.SendActivityAsync(
                        $"{response.Errors?.Count.ToQuantity("error")} occurred running the skill. " +
                        $"Visit {_urlGenerator.SkillPage(skill.Name)} to fix it.");
                }
            }
        }
        catch (Exception e) when (e is not SkillRunException)
        {
            // The exception only escapes if the org is not using a custom runner.
            throw new SkillRunException($"Exception occurred while calling {skill.Name}.",
                skill,
                messageContext.Organization.PlatformId,
                null,
                e,
                customRunner: false);
        }
    }

    async Task<SkillRunResponse> RunRemoteSkillAsync(
        MessageContext messageContext,
        string skillName,
        Skill skill,
        IArguments arguments,
        string commandText,
        IPattern? pattern,
        SkillRunProperties auditProperties)
    {
        var skillUrl = _urlGenerator.SkillPage(skill.Name);
        var chatConversation =
            messageContext.Conversation?.ToChatConversation(
                _urlGenerator.ConversationDetailPage(messageContext.Conversation.Id));

        var author = messageContext.FromMember;
        if (messageContext.InteractionInfo?.SourceMessage?.User is { } user)
        {
            author = await _slackResolver.ResolveMemberAsync(user, messageContext.Organization);
        }

        using var activity = AbbotTelemetry.ActivitySource.StartActivity($"{nameof(RemoteSkillCallSkill)}:InvokeSkill");
        return await _skillRunnerClient.SendAsync(
            skill,
            arguments,
            commandText,
            messageContext.Mentions,
            messageContext.FromMember,
            messageContext.Bot,
            messageContext.Room.ToPlatformRoom(),
            messageContext.Room.Customer?.ToCustomerInfo(),
            skillUrl,
            messageContext.IsInteraction,
            pattern,
            messageId: messageContext.MessageId,
            messageUrl: messageContext.MessageUrl,
            threadId: messageContext.ThreadId,
            triggeringMessageAuthor: author,
            conversation: chatConversation,
            room: messageContext.Room,
            interactionInfo: messageContext.InteractionInfo,
            auditProperties: auditProperties);
    }

    public async Task<string> GetSkillUsageText(string skillName, Organization organization)
    {
        var skill = await _skillRepository.GetAsync(skillName, organization);
        if (skill is null)
        {
            return $"Unknown skill {skillName}.";
        }

        return skill.UsageText is { Length: 0 }
            ? $"_This skill does not have any usage examples. Feel free to edit the skill at " +
              $"{_urlGenerator.SkillPage(skillName)} to add usage examples._"
            : skill.GetUsageText();
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{skill} {args}", "Calls the user created {skill} with the {args}.");
    }
}

static partial class RemoteSkillCallSkillLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to recognize arguments. {ProtectedArguments}")]
    public static partial void FailedToRecognizeArguments(
        this ILogger<RemoteSkillCallSkill> logger,
        Exception ex,
        string? protectedArguments);
}
