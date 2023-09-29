using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an example execution of a skill.
/// Used for Natural Language Argument Parsing (NLAP) and for other AI-triggered logic in the future.
/// </summary>
public class SkillExemplar : OrganizationEntityBase<SkillExemplar>, ISkillChildEntity
{
    /// <summary>
    /// The <see cref="Skill"/> to which this exemplar belongs.
    /// </summary>
    public required Skill Skill { get; set; }

    /// <summary>
    /// The ID of the <see cref="Skill"/> to which this exemplar belongs.
    /// </summary>
    public required int SkillId { get; set; }

    /// <summary>
    /// The text of the exemplar, used to train the model to recognize arguments
    /// </summary>
    public required string Exemplar { get; set; }

    /// <summary>
    /// Non-queryable properties associated with this exemplar.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ExemplarProperties Properties { get; set; } = new();
}

public record ExemplarProperties
{
    /// <summary>
    /// The OpenAI embedding vector for the exemplar.
    /// </summary>
    /// <remarks>
    /// This can be used to detect text similar to the exemplar by computing cosine similarity of the source text's embedding vector to this one.
    /// </remarks>
#pragma warning disable CA1819
    public double[]? EmbeddingVector { get; set; }
#pragma warning restore CA1819

    /// <summary>
    /// The argument string that represents the exemplar.
    /// If the exemplar is "show me the weather in Seattle", the arguments could be something like "location=Seattle".
    /// </summary>
    public string? Arguments { get; set; }
}
