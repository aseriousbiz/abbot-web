namespace Serious.Abbot.AI;

/// <summary>
/// An object intended to be passed as "PromptConstants" when rendering an AI prompt template.
/// This is essentially a set of static constants, but Handlebars doesn't provide access to statics.
/// </summary>
public class PromptConstants
{
#pragma warning disable CA1822
    public string AbbotLangDescription =>
#pragma warning restore CA1822
        """
        * An AbbotLang document is a JSON object with two properties: `thought` and `action`
        * `action` is a JSON array of Commands
        * Describe why you produced these actions as a JSON string in the `thought` property.
        * A Command is a JSON object with the `command` property set to the name of the command to be executed.
        * A Command has additional properties that are specific to the command being executed.
        * The Command name must match the Regular Expression `[\.a-z0-9_]+`
        """;

}
