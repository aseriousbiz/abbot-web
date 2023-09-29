using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Metadata;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// Routes incoming messages to the appropriate skill that will handle the message.
/// </summary>
public sealed class SkillRouter : ISkillRouter
{
    static readonly ILogger<SkillRouter> Log = ApplicationLoggerFactory.CreateLogger<SkillRouter>();

    readonly ISkillManifest _skillManifest;
    readonly ISkillPatternMatcher _skillPatternMatcher;
    readonly IPayloadHandlerRegistry _payloadHandlerRegistry;

    /// <summary>
    /// Constructs a <see cref="SkillRouter" />.
    /// </summary>
    /// <param name="skillManifest">The manifest of all skills.</param>
    /// <param name="skillPatternMatcher">Pattern matcher for skills.</param>
    /// <param name="payloadHandlerRegistry">A registry of payload handlers.</param>
    public SkillRouter(
        ISkillManifest skillManifest,
        ISkillPatternMatcher skillPatternMatcher,
        IPayloadHandlerRegistry payloadHandlerRegistry)
    {
        _skillManifest = skillManifest;
        _skillPatternMatcher = skillPatternMatcher;
        _payloadHandlerRegistry = payloadHandlerRegistry;
    }

    /// <summary>
    /// Retrieves a <see cref="RouteResult"/> for the incoming message. This result includes the resolved
    /// skill if any. Some commands to Abbot won't match a skill, but should be passed to the
    /// <see cref="ISkillNotFoundHandler"/>. In other cases the message should be ignored, in which case
    /// this returns a <see cref="RouteResult"/>.
    /// </summary>
    /// <param name="platformMessage">The incoming chat message.</param>
    /// <returns>A <see cref="RouteResult"/> with the result of routing.</returns>
    public async Task<RouteResult> RetrieveSkillAsync(IPlatformMessage platformMessage)
    {
        var text = platformMessage.Text.Trim();
        Log.MethodEntered(typeof(SkillRouter), nameof(RetrieveSkillAsync), text);

        if (platformMessage.Payload.Ignore)
        {
            // If the chat platform message says to ignore it, it means the message is a duplicate event, or otherwise completely unprocessable.
            // It does _not_ mean the message wasn't directed at Abbot.
            return RouteResult.Ignore;
        }

        var organization = platformMessage.Organization;
        var botUserId = platformMessage.Bot.UserId;
        var parsedMessage = await ParseMessage(platformMessage, text, botUserId, organization);

        // Only attempt to match patterns on normal messages, not interactive events nor direct messages.
        if (IsNormalMessage(parsedMessage, platformMessage)
            && await _skillPatternMatcher.GetMatchingPatternsAsync(
                platformMessage,
                platformMessage.From,
                organization) is { Count: > 0 } matchingPatterns)
        {
            parsedMessage = ParsedMessage.Create(matchingPatterns, platformMessage.Text);
        }

        if (parsedMessage is null)
        {
            // This indicates the incoming event is an interactive event that _did not match_ a known Skill ID.
            // That means we should indeed just ignore this message.
            return RouteResult.Ignore;
        }

        if (parsedMessage.IsInteraction)
        {
            return await ReturnInteractionResultAsync(platformMessage, parsedMessage, parsedMessage.Skill);
        }

        if (parsedMessage.PotentialSkillName is { Length: > 0 }
            // User is attempting to call a skill, and the call is in the correct format.
            // So let's see if it matches an actual skill.
            && await _skillManifest.ResolveSkillAsync(
                parsedMessage.PotentialSkillName,
                organization,
                platformMessage) is { } matchedSkill)
        {
            return HandleSkillInvocationAsync(platformMessage, parsedMessage, matchedSkill);
        }

        var messageContext = CreateMessageContext(parsedMessage, platformMessage);
        return new RouteResult(messageContext, Skill: null, parsedMessage.IsBotCommand, IsPatternMatch: false);
    }

    // A normal conversational message that's not a bot command, a direct message, a workflow message, nor an
    // interaction.
    static bool IsNormalMessage(ParsedMessage? parsedMessage, IPlatformMessage platformMessage) =>
        parsedMessage?.IsBotCommand is not true
               && platformMessage is { DirectMessage: false, Payload.WorkflowMessage: false }
               && parsedMessage?.IsInteraction != true;

    async Task<ParsedMessage?> ParseMessage(
        IPlatformMessage platformMessage,
        string text,
        string botUserId,
        Organization organization)
    {
        var parsedMessage = platformMessage.Payload.InteractionInfo is { } interactionInfo
            ? await ParseInteractiveEvent(interactionInfo.CallbackInfo, interactionInfo.Arguments)
            : !platformMessage.Payload.WorkflowMessage
                ? ParsedMessage.Parse(text, botUserId, organization.ShortcutCharacter)
                : ParsedMessage.CreateMessageNotForBot(text);
        return parsedMessage;
    }

    static RouteResult HandleSkillInvocationAsync(IPlatformMessage platformMessage, ParsedMessage parsedMessage,
        IResolvedSkill matchedSkill)
    {
        var messageContext = CreateMessageContext(parsedMessage, platformMessage);
        return parsedMessage.ContextId != messageContext.ContextId
            ? RouteResult.Ignore
            : platformMessage.CanInvokeSkillDirectly() ||
              parsedMessage.Patterns.Any() // We already filter out patterns that are not externally callable
                ? new RouteResult(
                    messageContext.WithResolvedSkill(matchedSkill),
                    matchedSkill.Skill,
                    parsedMessage.IsBotCommand,
                    IsPatternMatch: parsedMessage.Patterns.Any())
                : RouteResult.Ignore; // Foreign members cannot call skills.
    }

    async Task<RouteResult> ReturnInteractionResultAsync(
        IPlatformMessage platformMessage,
        ParsedMessage parsedMessage,
        Skill skill)
    {
        var messageContext = CreateMessageContext(parsedMessage, platformMessage);

        if (parsedMessage.ContextId.ToNullIfEmpty() != messageContext.ContextId)
        {
            return RouteResult.Ignore;
        }

        var matchedSkill = await _skillManifest.ResolveSkillAsync(
            skill,
            appendedArguments: string.Empty,
            platformMessage);

        // Even if the user is in a foreign organization, we want them to be able to interact
        // with UI elements presented by a skill.
        return new RouteResult(
            messageContext.WithResolvedSkill(matchedSkill),
            matchedSkill.Skill,
            IsDirectedAtBot: true,
            IsPatternMatch: false);
    }

    public PayloadHandlerRouteResult RetrievePayloadHandler(IPlatformEvent platformEvent)
    {
        var handler = _payloadHandlerRegistry.Retrieve(platformEvent);
        return handler is null ? PayloadHandlerRouteResult.Ignore : new PayloadHandlerRouteResult(handler);
    }

    async Task<ParsedMessage?> ParseInteractiveEvent(CallbackInfo callbackInfo, string arguments)
    {
        async Task<ParsedMessage?> GetUserSkillParsedMessage(UserSkillCallbackInfo userSkillCallbackInfo)
        {
            var (skillId, contextId) = userSkillCallbackInfo;
            var skill = await _skillManifest.GetSkillByIdAsync(skillId);
            return skill is null
                ? null
                : ParsedMessage.Create(skill, arguments, contextId);
        }

        ParsedMessage GetBuiltInParsedMessage(BuiltInSkillCallbackInfo builtInSkillCallbackInfo)
        {
            var (skillName, contextId) = builtInSkillCallbackInfo;
            return ParsedMessage.Create(skillName, arguments, contextId);
        }

        return callbackInfo switch
        {
            UserSkillCallbackInfo userSkillCallbackInfo => await GetUserSkillParsedMessage(userSkillCallbackInfo),
            BuiltInSkillCallbackInfo builtInSkillCallbackInfo => GetBuiltInParsedMessage(builtInSkillCallbackInfo),
            _ => null
        };
    }

    static MessageContext CreateMessageContext(ParsedMessage parsedMessage, IPlatformMessage platformMessage)
        => new(
            platformMessage,
            parsedMessage.PotentialSkillName,
            parsedMessage.PotentialArguments,
            parsedMessage.CommandText,
            parsedMessage.OriginalMessage,
            parsedMessage.Sigil,
            parsedMessage.Patterns,
            parsedMessage.Skill?.Scope);
}
