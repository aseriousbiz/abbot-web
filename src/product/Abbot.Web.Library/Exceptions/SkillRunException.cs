using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Exceptions;

/// <summary>
/// Exception thrown when an error occurs while parsing a script.
/// </summary>
#pragma warning disable CA1032
public sealed class SkillRunException : Exception
#pragma warning restore CA1032
{
    /// <summary>
    /// Constructs a <see cref="SkillRunException"/>.
    /// </summary>
    /// <param name="message">A message about running the skill.</param>
    /// <param name="skill">The skill we're trying to run.</param>
    /// <param name="platformId">The platform Id.</param>
    /// <param name="endpoint">The Url of the skill runner.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="customRunner">Indicates if this exception occurred within a custom runner.</param>
    public SkillRunException(
        string message,
        Skill skill,
        string platformId,
        Uri? endpoint,
        Exception? innerException,
        bool customRunner) : base(message, innerException)
    {
        Skill = skill;
        PlatformId = platformId;
        Endpoint = endpoint;
        CustomRunner = customRunner;
    }

    /// <summary>
    /// The platform specific ID of the organization that owns the skill being called.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// The skill that was being called when the exception occurred.
    /// </summary>
    public Skill Skill { get; }

    /// <summary>
    /// The skill endpoint, if known.
    /// </summary>
    public Uri? Endpoint { get; }

    /// <summary>
    /// Indicates if this exception occurred within a custom runner.
    /// </summary>
    public bool CustomRunner { get; }
}
