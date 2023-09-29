using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Serious.Abbot.Pages.Skills.Subscriptions;

/// <summary>
/// For the Pattern creation and edit pages, this contains the values of the incoming form post via Model Binding.
/// </summary>
public class SignalSubscriptionInputModel
{
    /// <summary>
    /// THe Regex pattern used to validate signal name when subscribing to a signal.
    /// </summary>
    /// <remarks>
    /// We don't care about the max length really because that's enforced on creation of the signal. But we can
    /// create system signals of arbitrary length and we want users to be able to subscribe to them. So
    /// 128 is an arbitrary limit that should be more than enough.
    /// </remarks>
    public const string SignalNamePattern = @"^[a-zA-Z0-9](?:[a-zA-Z0-9:]|-(?=[a-zA-Z0-9])){0,128}$";

    /// <summary>
    /// The name of the signal to subscribe to.
    /// </summary>
    [Remote(action: "ValidateName", controller: "SignalValidation", areaName: "InternalApi", AdditionalFields = nameof(Skill))]
    [StringLength(128, MinimumLength = 2, ErrorMessage = "Valid signal names must have at least 2 characters.")]
    [RegularExpression(SignalNamePattern,
        ErrorMessage = "Name may only contain a-z and 0-9. For multi-word names, separate the words by a dash character.")]
    [Display(Name = "Signal", Description = "The signal this skill responds to.")]
    public string Name { get; set; } = null!;

    [Display(Description = "The pattern to match against signal arguments.")]
    [Remote(action: "ValidatePattern", controller: "Patterns", areaName: "InternalApi", AdditionalFields = nameof(PatternType))]
    public string? Pattern { get; set; }

    [Display(Name = "Pattern Type", Description = "The pattern type.")]
    public PatternType PatternType { get; set; }

    [Display(Description = "Whether the pattern should be applied in a case sensitive manner or not.")]
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// The current skill for this pattern. Not meant to be displayed as a form field,
    /// but used to help validate unique signal name.
    /// </summary>
    public string Skill { get; set; } = null!;
}
