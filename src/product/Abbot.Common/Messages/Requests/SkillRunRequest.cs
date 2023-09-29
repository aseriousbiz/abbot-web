namespace Serious.Abbot.Messages;

/// <summary>
/// Body of the request by a client to run a skill. This is used by the the skill editor or abbot cli
/// to run skill code. It's also used by the signal handler. It allows testing current
/// changes to code without having to save the code.
/// </summary>
public class SkillRunRequest
{
    /// <summary>
    /// The current skill name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The skill arguments to pass.
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// The code to run
    /// </summary>
    public required string Code { get; set; }
}
