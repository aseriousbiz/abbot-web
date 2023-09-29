using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Serialization;
using Serious.Logging;

namespace Serious.Abbot.Playbooks;

public class StepContext
{
    static readonly ILogger<StepContext> Log = ApplicationLoggerFactory.CreateLogger<StepContext>();

    public static readonly AbbotJsonFormat JsonFormat = new NewtonsoftJsonAbbotJsonFormat(new()
    {
        Error = (object? sender, ErrorEventArgs e) => {
            e.ErrorContext.Handled = true;
            Log.IgnoredSerializationError(e.ErrorContext.Error);
        },
    });

    public StepContext(ConsumeContext consumeContext)
    {
        ConsumeContext = consumeContext;
    }

    public ConsumeContext ConsumeContext { get; }
    public required ActionReference ActionReference { get; init; }
    public required ActionStep Step { get; init; }
    public required IDictionary<string, object?> Inputs { get; init; }
    public required Playbook Playbook { get; init; }
    public required PlaybookRun PlaybookRun { get; init; }

    public ITemplateEvaluator? TemplateEvaluator { get; init; }

    public IDictionary<string, object?>? ResumeState { get; init; }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public T? Get<T>(string name, T? defaultValue = default, string? oldName = null) where T : notnull
    {
        using var _ = Log.BeginInputScope(name, oldName);
        var result = Inputs.TryGetValue(name, out var value)
            ? JsonFormat.Convert<T>(value)
            : default;

        if (result is null && oldName is not null)
        {
            result = Inputs.TryGetValue(oldName, out var oldValue)
                ? JsonFormat.Convert<T>(oldValue)
                : default;
        }

        return result ?? defaultValue;
    }

    public T Expect<T>(string name, string? oldName = null) where T : notnull
    {
        using var _ = Log.BeginInputScope(name, oldName);
        object? oldValue = null;
        if (!Inputs.TryGetValue(name, out var value)
            & (oldName is null || !Inputs.TryGetValue(oldName, out oldValue)))
        {
            throw new ValidationException($"Input '{name}' is required.");
        }

        return JsonFormat.Convert<T>(value) ??
            JsonFormat.Convert<T>(oldValue) ??
            throw new ValidationException($"Input '{name}' must be convertible to {typeof(T).Name}.");
    }

    public T ExpectParseable<T>(string name) where T : IParsable<T>
    {
        if (!Inputs.TryGetValue(name, out var value))
        {
            throw new ValidationException($"Input '{name}' is required.");
        }

        if (value is T parsed)
        {
            return parsed;
        }

        if (value is not string s)
        {
            throw new ValidationException($"The value is not a string.");
        }

        return T.Parse(s, null);
    }

    public bool TryGetUnprotectedApiToken(
        [NotNullWhen(true)] out string? apiToken,
        [NotNullWhen(false)] out StepResult? noTokenResult)
    {
        if (!Playbook.Organization.TryGetUnprotectedApiToken(out apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            noTokenResult = new StepResult(StepOutcome.Failed)
            {
                Problem = Problems.SlackApiTokenMissing()
            };
            return false;
        }

        noTokenResult = null;
        return true;
    }
}

public static class StepContextExtensions
{
    public static (string? ChannelId, string? MessageId) GetMessageTarget(this StepContext context, string name, string? oldName = null)
    {
        var target = context.Get<string>(name, oldName: oldName);

        if (target is not null
            && SlackUrl.TryParse(target, out var slackUrl)
            && slackUrl is SlackMessageUrl url)
        {
            return (url.ConversationId, url.ThreadTimestamp ?? url.Timestamp);
        }

        return (target, null);
    }

    public static (string ChannelId, string? MessageId) ExpectMessageTarget(this StepContext context, string name, string? oldName = null)
    {
        var (channelId, messageId) = context.GetMessageTarget(name, oldName);
        // Checking name again to throw with preferred name
        channelId ??= context.Expect<string>(name, oldName);
        return (channelId, messageId);
    }

    /// <summary>
    /// Gets the value as a <see cref="TipTapDocument"/> and renders it as mrkdwn, but if the value is not a
    /// document, assumes it is already mrkdwn.
    /// </summary>
    /// <param name="context">The <see cref="StepContext"/>.</param>
    /// <param name="name">The name of the property to get.</param>
    /// <param name="oldName">The old name of the property to get.</param>
    /// <returns>A mrkdwn string.</returns>
    public static string ExpectMrkdwn(this StepContext context, string name, string? oldName = null)
    {
        return context.Get<TipTapDocument>(name)?.ToMrkdwn(context.TemplateEvaluator)
            ?? context.Get<string>(name)
            ?? (oldName is null ? null : context.Get<TipTapDocument>(oldName)?.ToMrkdwn(context.TemplateEvaluator))
            // Checking name again to throw with preferred name
            ?? context.Expect<string>(name, oldName);
    }
}

static partial class StepContextLoggerExtensions
{
    static readonly Func<ILogger, string, string?, IDisposable?> InputScope =
        LoggerMessage.DefineScope<string, string?>(
            "Input name: {InputName} (Old: {OldInputName})");

    public static IDisposable? BeginInputScope(this ILogger<StepContext> logger, string name, string? oldName = null) =>
        InputScope(logger, name, oldName);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Ignored serialization error.")]
    public static partial void IgnoredSerializationError(
        this ILogger<StepContext> logger,
        Exception ex);
}
