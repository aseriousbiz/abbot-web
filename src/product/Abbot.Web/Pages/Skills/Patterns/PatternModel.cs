using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Pages.Skills.Patterns;

/// <summary>
/// Base class for <see cref="PatternInputModel" /> used to edit and create patterns and the
/// <see cref="PatternTestInputModel" /> used to test a pattern.
/// </summary>
public abstract class PatternModel
{
    /// <summary>
    /// Id of the pattern. Not meant to be displayed as a form field, but used to help validate unique
    /// pattern name.
    /// </summary>
    public int? Id { get; set; }

    [Display(Description = "Required. The pattern to match against chat messages.")]
    [Remote(action: "ValidatePattern", controller: "Patterns", areaName: "InternalApi", AdditionalFields = "PatternType")]
    public string Pattern { get; set; } = null!;

    [Display(Name = "Pattern Type", Description = "Required. The pattern type.")]
    public PatternType PatternType { get; set; }

    [Display(Description = "Whether the pattern should be applied in a case sensitive manner or not.")]
    public bool CaseSensitive { get; set; }

    [Display(Description = "Whether the pattern should be enabled.")]
    public bool Enabled { get; set; } = true;

    [Display(Description = "Whether the pattern should be match messages from external users.")]
    public bool AllowExternalCallers { get; set; }
}
