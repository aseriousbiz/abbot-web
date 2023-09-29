using System;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Entities;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Text;

namespace Serious.Abbot.Events;

/// <summary>
/// Information about the skill to call in response to an interactive event.
/// </summary>
public abstract record CallbackInfo()
{
    readonly string _callbackId = string.Empty;

    protected CallbackInfo(char prefix, string callbackInfo, string? contextId) : this()
    {
        _callbackId = contextId is null
            ? $"{prefix}:{callbackInfo}"
            : $"{prefix}:{callbackInfo}:{contextId}";

        if (_callbackId.Length > ViewBase.CallbackIdMaxLength)
        {
            throw new InvalidOperationException($"The callback id is too damn long. It may only be {ViewBase.CallbackIdMaxLength} characters long.\n{_callbackId}");
        }
    }

    /// <summary>
    /// Returns a formatted callback id.
    /// </summary>
    /// <returns>A delimited callback id.</returns>
    public override string ToString()
    {
        return _callbackId;
    }

    /// <summary>
    /// Attempts to parse a callback id as a derived type of <see cref="CallbackInfo"/>.
    /// </summary>
    /// <param name="callbackId">The callback id.</param>
    /// <param name="callbackInfo">The callback info.</param>
    /// <typeparam name="TCallbackInfo">The <see cref="CallbackInfo"/> type.</typeparam>
    /// <returns><c>true</c> if the callback id is a <see cref="CallbackInfo"/> of the specified type.</returns>
    public static bool TryParseAs<TCallbackInfo>(string? callbackId, [NotNullWhen(true)] out TCallbackInfo? callbackInfo)
        where TCallbackInfo : CallbackInfo
    {
        if (TryParse(callbackId, out var cb) && cb is TCallbackInfo outValue)
        {
            callbackInfo = outValue;
            return true;
        }

        callbackInfo = null;
        return false;
    }

    /// <summary>
    /// Given an element in a <c>block_actions</c> payload (<see cref="IPayloadElement"/>), attempts to retrieve the
    /// <see cref="CallbackInfo" /> from the Action ID. If that fails, tries the Block Id.
    /// </summary>
    /// <param name="payloadElement">The payload element.</param>
    /// <param name="interactionCallbackInfo">The resulting <see cref="InteractionCallbackInfo"/>.</param>
    /// <returns></returns>
    public static bool TryGetCallbackInfoPayloadElement<TCallbackInfo>(
        IPayloadElement payloadElement,
        [NotNullWhen(true)] out TCallbackInfo? interactionCallbackInfo) where TCallbackInfo : CallbackInfo
    {
        return TryParseAs(Unwrap(payloadElement.ActionId), out interactionCallbackInfo)
               || TryParseAs(Unwrap(payloadElement.BlockId), out interactionCallbackInfo);
    }

    /// <summary>
    /// Attempts to parse a callback Id into a <see cref="CallbackInfo"/>.
    /// </summary>
    /// <param name="callbackId">The callback id.</param>
    /// <param name="callbackInfo">The callback info.</param>
    /// <returns><c>true</c> if the callback id is a <see cref="CallbackInfo"/>.</returns>
    public static bool TryParse(string? callbackId, [NotNullWhen(true)] out CallbackInfo? callbackInfo)
    {
        callbackInfo = null;
        if (callbackId is null or { Length: < 3 } || callbackId[1] != ':')
        {
            return false;
        }

        if (callbackId.Length > ViewBase.CallbackIdMaxLength)
        {
            throw new InvalidOperationException($"The callback id is too damn long. It may only be {ViewBase.CallbackIdMaxLength} characters long.\n{callbackId}");
        }

        var prefix = callbackId[0];    // First character is the prefix.
        var target = callbackId[2..]; // The rest is the target.
        var parts = target.Split(':', 2);
        var identifier = parts[0];
        var contextId = parts.Length > 1 ? parts[1] : null;

        callbackInfo = prefix switch
        {
            UserSkillCallbackInfo.Prefix when Id<Skill>.TryParse(identifier, out var skillId) => new UserSkillCallbackInfo(skillId, contextId),
            BuiltInSkillCallbackInfo.Prefix => new BuiltInSkillCallbackInfo(identifier, contextId),
            InteractionCallbackInfo.Prefix => new InteractionCallbackInfo(identifier, contextId),
            _ => null
        };

        return callbackInfo is not null;
    }

    static string? Unwrap(string? value) => value is null ? null : WrappedValue.Parse(value).ExtraInformation;

    /// <summary>
    /// Implicit conversion to <c>string</c>.
    /// </summary>
    /// <param name="callbackInfo">The <see cref="CallbackInfo"/> to represent as a string.</param>
    /// <returns>A delimited string containing the callback info.</returns>
    public static implicit operator string(CallbackInfo callbackInfo) => callbackInfo.ToString();
}

/// <summary>
/// Information about a built-in skill to call in response to an interactive event.
/// </summary>
/// <param name="SkillName">The name of the skill.</param>
/// <param name="ContextId">Additional context to pass.</param>
public record BuiltInSkillCallbackInfo(string SkillName, string? ContextId = null)
    : CallbackInfo(Prefix, SkillName, ContextId)
{
    public const char Prefix = 'b';

    public override string ToString() => base.ToString();
}

/// <summary>
/// Information about a user skill to call in response to an interactive event.
/// </summary>
/// <param name="SkillId">The Id of the skill.</param>
/// <param name="ContextId">Additional context to pass.</param>
public record UserSkillCallbackInfo(Id<Skill> SkillId, string? ContextId = null)
    : CallbackInfo(Prefix, $"{SkillId}", ContextId)
{
    public const char Prefix = 's';

    public override string ToString() => base.ToString();
}

/// <summary>
/// Information about the handler to call in response to an interactive event.
/// </summary>
/// <param name="TypeName">The handler type name.</param>
/// <param name="ContextId">Additional context to pass.</param>
public record InteractionCallbackInfo(string TypeName, string? ContextId = null)
    : CallbackInfo(Prefix, TypeName, ContextId)
{
    public const char Prefix = 'i';

    public override string ToString() => base.ToString();

    public static InteractionCallbackInfo For<T>(string? contextId = null)
        where T : IHandler =>
        new(typeof(T).Name, contextId);
}
