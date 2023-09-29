// Copied from https://github.com/dotnet/aspnetcore/blob/352790912f771469c2d3cda692ebd2c2b26ffc76/src/Mvc/Mvc.NewtonsoftJson/src/AnnotatedProblemDetails.cs
// And: https://github.com/dotnet/aspnetcore/blob/352790912f771469c2d3cda692ebd2c2b26ffc76/src/Mvc/Mvc.NewtonsoftJson/src/ProblemDetailsConverter.cs
//
// The built-in MVC ProblemDetails is annotated for System.Text.Json, but we use JSON.NET in MassTransit.
// This ensures we get the right serialization behavior in MT.

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Serious.Abbot.Eventing.Infrastructure;

internal class AnnotatedProblemDetails
{
    /// <remarks>
    /// Required for JSON.NET deserialization.
    /// </remarks>
    public AnnotatedProblemDetails() { }

    public AnnotatedProblemDetails(ProblemDetails problemDetails)
    {
        Detail = problemDetails.Detail;
        Instance = problemDetails.Instance;
        Status = problemDetails.Status;
        Title = problemDetails.Title;
        Type = problemDetails.Type;

        foreach (var kvp in problemDetails.Extensions)
        {
            Extensions[kvp.Key] = kvp.Value;
        }
    }

    [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty(PropertyName = "title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty(PropertyName = "status", NullValueHandling = NullValueHandling.Ignore)]
    public int? Status { get; set; }

    [JsonProperty(PropertyName = "detail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Detail { get; set; }

    [JsonProperty(PropertyName = "instance", NullValueHandling = NullValueHandling.Ignore)]
    public string? Instance { get; set; }

    [JsonExtensionData]
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public void CopyTo(ProblemDetails problemDetails)
    {
        problemDetails.Type = Type;
        problemDetails.Title = Title;
        problemDetails.Status = Status;
        problemDetails.Instance = Instance;
        problemDetails.Detail = Detail;

        foreach (var kvp in Extensions)
        {
            problemDetails.Extensions[kvp.Key] = kvp.Value;
        }
    }
}

/// <summary>
/// A RFC 7807 compliant <see cref="JsonConverter"/> for <see cref="ProblemDetails"/>.
/// </summary>
public sealed class ProblemDetailsConverter : JsonConverter
{
    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(ProblemDetails);
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var annotatedProblemDetails = serializer.Deserialize<AnnotatedProblemDetails>(reader);
        if (annotatedProblemDetails == null)
        {
            return null;
        }

        var problemDetails = (ProblemDetails?)existingValue ?? new ProblemDetails();
        annotatedProblemDetails.CopyTo(problemDetails);

        return problemDetails;
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var problemDetails = (ProblemDetails)value;
        var annotatedProblemDetails = new AnnotatedProblemDetails(problemDetails);

        serializer.Serialize(writer, annotatedProblemDetails);
    }
}
