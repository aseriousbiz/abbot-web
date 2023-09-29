using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

[assembly: CLSCompliant(false)]
namespace Serious.Slack;

/// <summary>
/// Base type for all Slack entities returned by the API.
/// </summary>
public abstract record EntityBase
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    protected EntityBase()
    {
    }

    /// <summary>
    /// Constructor with an Id.
    /// </summary>
    /// <param name="id"></param>
    protected EntityBase(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The Slack Id of the entity.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
