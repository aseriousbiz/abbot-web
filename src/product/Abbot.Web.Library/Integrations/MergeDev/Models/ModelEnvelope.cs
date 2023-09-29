using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.MergeDev.Models;

/// <summary>
/// Envelope for <c>POST</c>/<c>PATCH</c> results.
/// </summary>
/// <typeparam name="T"></typeparam>
public record ModelEnvelope<T>(
    [property:JsonProperty("model")]
    [property:JsonPropertyName("model")]
    T? Model
) where T : class
{
}
