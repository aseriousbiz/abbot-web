using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Entities;

/// <summary>
/// The programming language for a skill.
/// </summary>
public enum CodeLanguage
{
    [Display(Name = "C#")]
    CSharp = 0,
    Python = 1,
    [Display(Name = "JavaScript")]
    JavaScript = 2,
    Ink = 3,
    None = int.MaxValue,
}
