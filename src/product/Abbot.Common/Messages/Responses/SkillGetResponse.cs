using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Messages;

/// <summary>
/// The response from calling https://api.ab.bot/api/cli/{skill}/edit. This is used by the Abbot CLI to
/// retrieve info about a skill and start an editing session.
/// </summary>
public class SkillGetResponse
{
    /// <summary>
    /// The skill name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The code for the skill.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// A SHA1 hash of the code used to uniquely identify the code and determine if the code has changed.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// The programming language of the skill.
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// Whether the skill is enabled or not.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The date this was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// The user that last modified the skill.
    /// </summary>
    public UserGetResponse LastModifiedBy { get; set; } = null!;
}
