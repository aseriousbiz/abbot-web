using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Skills.Patterns;

/// <summary>
/// For the Pattern creation and edit pages, this contains the values of the incoming form post via Model Binding.
/// </summary>
public class PatternInputModel : PatternModel
{
    [Remote(action: "ValidateName", controller: "Patterns", areaName: "InternalApi", AdditionalFields = "Id,Skill")]
    [Display(Description = "Required. This is a descriptive name used to refer to a pattern.")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The current skill for this pattern. Not meant to be displayed as a form field,
    /// but used to help validate unique pattern name.
    /// </summary>
    public string Skill { get; set; } = null!;

    /// <summary>
    /// Updates the <see cref="SkillPattern"/> entity with the values of this input model.
    /// </summary>
    /// <param name="pattern">The pattern to update.</param>
    public void UpdateSkillPattern(SkillPattern pattern)
    {
        pattern.Name = Name;
        pattern.Pattern = Pattern;
        pattern.PatternType = PatternType;
        pattern.CaseSensitive = CaseSensitive;
        pattern.Enabled = Enabled;
        pattern.AllowExternalCallers = AllowExternalCallers;
    }

    /// <summary>
    /// Initializes the values of this input model from an existing <see cref="SkillPattern"/>.
    /// </summary>
    /// <param name="pattern">The source pattern.</param>
    public void Initialize(SkillPattern pattern)
    {
        Id = pattern.Id;
        Name = pattern.Name;
        Pattern = pattern.Pattern;
        PatternType = pattern.PatternType;
        CaseSensitive = pattern.CaseSensitive;
        Enabled = pattern.Enabled;
        Skill = pattern.Skill.Name;
        AllowExternalCallers = pattern.AllowExternalCallers;
    }
}
