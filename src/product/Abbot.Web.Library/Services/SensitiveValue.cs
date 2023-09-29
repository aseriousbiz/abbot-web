using System.Text.RegularExpressions;
using Azure.AI.TextAnalytics;
using Newtonsoft.Json;

namespace Serious.Abbot.Services;

/// <summary>
/// A word or phrase identified as a Personally Identifiable Information
/// that can be categorized as known type in a given taxonomy.
/// The set of categories recognized by the Language service is described at
/// <see href="https://aka.ms/tanerpii"/>.
/// </summary>
/// <param name="Text">Gets the entity text as it appears in the input document.</param>
/// <param name="Category">
/// Gets the PII entity category inferred by the Text Analytics service's
/// named entity recognition model, such as Financial Account
/// Identification/Social Security Number/Phone Number, etc.
/// The list of available categories is described at
/// <see href="https://aka.ms/tanerpii"/>.
/// </param>
/// <param name="Subcategory">
/// Gets the subcategory of the entity inferred by the Language service's
/// named entity recognition model.  This property may not have a value if
/// a subcategory doesn't exist for this entity.  The list of available categories and
/// subcategories is described at <see href="https://aka.ms/tanerpii"/>.
/// </param>
/// <param name="ConfidenceScore">Gets a score between 0 and 1, indicating the confidence that the
/// text substring matches this inferred entity.</param>
/// <param name="Offset">Gets the starting position for the matching text in the input document.</param>
/// <param name="Length">Gets the length of the matching text in the input document.</param>
public record struct SensitiveValue(
    string Text,
    [property: JsonConverter(typeof(PiiEntityCategoryConverter))]
    PiiEntityCategory Category,
    string? Subcategory,
    double ConfidenceScore,
    int Offset,
    int Length)
{
    /// <summary>
    /// Wraps a <see cref="PiiEntity"/>.
    /// </summary>
    /// <param name="entity">A <see cref="PiiEntity"/>.</param>
    public static SensitiveValue Create(PiiEntity entity) =>
        new(entity.Text, entity.Category, entity.SubCategory, entity.ConfidenceScore, entity.Offset, entity.Length);

    /// <summary>
    /// Creates a <see cref="SensitiveValue" /> from a regex match.
    /// </summary>
    /// <param name="match">The <see cref="Match"/> from a regular expression.</param>
    /// <param name="category">The category for the match.</param>
    public static SensitiveValue FromRegexMatch(Match match, PiiEntityCategory category) =>
        new(match.Value, category, null, 0.9, match.Index, match.Length);
}

public class PiiEntityCategoryConverter : JsonConverter<PiiEntityCategory?>
{
    public override void WriteJson(JsonWriter writer, PiiEntityCategory? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value.ToString());
        }
    }

    public override PiiEntityCategory? ReadJson(
        JsonReader reader,
        Type objectType,
        PiiEntityCategory? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonToken.String)
        {
            throw new JsonSerializationException(
                $"Cannot deserialize a {reader.TokenType} into a {nameof(PiiEntityCategory)}");
        }
        var value = (string?)reader.Value;

        return value is { Length: > 0 } ? new(value) : null;
    }
}
