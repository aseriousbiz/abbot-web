using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.AI.Templating;

public record TemplatedPrompt
{
    /// <summary>
    /// The version of the prompt
    /// </summary>
    public required PromptVersion Version { get; init; }

    /// <summary>
    /// The prompt text.
    /// </summary>
    public required string Text { get; init; }
}

public enum PromptVersion
{
    [Display(Name = "Version 1 (Token Replacement)")]
    Version1,

    [Display(Name = "Version 2 (Handlebars)")]
    Version2,
}
