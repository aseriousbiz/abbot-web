using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for Slack elements such as <see cref="ButtonElement"/>, <see cref="ImageElement"/>, etc.
/// </summary>
public abstract record Element() : IElement, IPropertyBag
{
    /// <summary>
    /// Constructs an <see cref="Element"/>.
    /// </summary>
    /// <param name="type">The element <c>type</c>.</param>
    protected Element(string type) : this()
    {
        Type = type;
    }

    /// <summary>
    /// The element <c>type</c>.
    /// </summary>
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; } = string.Empty;

    /// <summary>
    /// The additional properties of the unknown element.
    /// </summary>
    [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
    [System.Text.Json.Serialization.JsonExtensionData]
    // ReSharper disable once CollectionNeverUpdated.Global
    IDictionary<string, object> IPropertyBag.AdditionalProperties { get; } =
        new Dictionary<string, object>();
}
