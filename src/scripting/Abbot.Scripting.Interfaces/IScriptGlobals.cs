namespace Serious.Abbot.Scripting;

/// <summary>
/// The global object for all user skills.
/// </summary>
public interface IScriptGlobals
{
    /// <summary>
    /// It's Abbot! Provides a set of services and information for your bot skill.
    /// </summary>
    IBot Bot { get; }
}
