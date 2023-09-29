using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Pages.Skills.Patterns;

/// <summary>
/// For the Pattern creation and edit pages, this is used to test a pattern by posting to api/internal/patterns
/// (<see cref="PatternsController.TestAsync"/>).
/// </summary>
public class PatternTestInputModel : PatternModel, IPatternMatchableMessage
{
    /// <summary>
    /// The message to test.
    /// </summary>
    [Display(Description = "The message to test against your pattern")]
    [Required]
    public string Message { get; set; } = null!;

    [BindNever]
    public string Text => Message;
}
