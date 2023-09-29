using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Execution;

/// <summary>
/// Helper methods for creating responses from the .NET skill runner.
/// </summary>
public static class SkillRunResponseFactory
{
    public static SkillRunResponse CreateSuccess(IEnumerable<string> replies, IReadOnlyDictionary<string, object?> outputs)
    {
        return CreateTriggerResponse(
            true,
            null,
            null,
            replies,
            Array.Empty<RuntimeError>(),
            outputs);
    }

    public static SkillRunResponse CreateFailed(IEnumerable<RuntimeError> errors)
    {
        return CreateTriggerResponse(
            false,
            null,
            null,
            Array.Empty<string>(),
            errors,
            new Dictionary<string, object?>());
    }

    static SkillRunResponse CreateTriggerResponse(
        bool success,
        string? contentType,
        string? content,
        IEnumerable<string> replies,
        IEnumerable<RuntimeError> errors,
        IReadOnlyDictionary<string, object?> outputs)
    {
        return new()
        {
            Success = success,
            Replies = replies.ToList(),
            Errors = errors.ToList(),
            ContentType = contentType,
            Content = content,
            Outputs = outputs,
        };
    }
}
