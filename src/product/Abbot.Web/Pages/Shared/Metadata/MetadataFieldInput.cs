using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// Input model for the Metadata Fields.
/// </summary>
public class MetadataFieldInput
{
    [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
    [Remote(action: "Validate",
        controller: "MetadataFieldValidation",
        areaName: "InternalApi", AdditionalFields = "Type")]
    public string Name { get; init; } = null!;

    public MetadataFieldType Type { get; init; }

    public string? DefaultValue { get; init; }
}
